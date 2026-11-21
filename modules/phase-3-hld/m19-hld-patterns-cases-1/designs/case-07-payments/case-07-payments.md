# Case 7: Payment system (a store's Payment Service)

Diagram: [case-07-payments.excalidraw](case-07-payments.excalidraw), in two rows: **A. Normal checkout** (every path) and **B. Flash sale** (5k payments/s; same layout, only what differs, numbered in order). Caller (Order Service) and processor webhooks → APIM (tall box) → a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design a payment system, like the payments service behind an online store."

**Framing (agreed):** we are the store's **Payment Service**. We don't talk to card networks or banks; an external **card processor** (Stripe / Adyen) does. Our job is to use it **correctly**: never lose a payment, never charge twice, and prove it every day.

## Functional requirements (in scope; learner's list, 2026-11-21)
1. **Charge a customer** for an order: **authorize** (reserve the money), then **capture** (take it), immediately or later (e.g. when the order ships).
2. **Never charge twice**, despite retries, timeouts and crashes on any side.
3. **Refund** a captured payment, fully or partly (several partial refunds allowed, never more than was captured).
4. **Reconcile daily:** compare our payments and refunds with the processor's settlement report and flag every mismatch.

**Out of scope (say this out loud):** a double-entry ledger, seller payouts, fraud detection, currency conversion, subscriptions, storing card numbers (the processor holds them; we keep only tokens, which keeps us out of most PCI scope), disputes / chargebacks beyond recording the webhook.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| **No double charge** | Effectively once: at-least-once delivery + idempotency at every hop | The worst bug: a refund, a support ticket and lost trust |
| **No lost payment** | Every accepted request ends in a final state (authorized / captured / failed / refunded) | "Unknown" must be resolved, never forgotten |
| Consistency | **CP** for payment state: if unsure, say "pending", never guess | Correctness beats availability for money |
| Latency | Authorize p99 < 2 s (dominated by the processor) | The shopper is waiting on the checkout page |
| Auditability | Every state change kept with who / when / why, for **7 years** | Finance and regulators ask |
| Availability | 99.95%; on processor trouble, fail **closed** (queue or decline), never "assume success" | — |

## Capacity estimate (high level)
**Facts:** 10M orders/day; ~3 processor calls per payment (authorize, capture, sometimes a query) + ~2 webhooks; 2% refunded; peak = 3× average; 1 day ≈ 10⁵ s. Flash sale: 5k payments/s for 10 min; the processor allows ~2k calls/s per account.

| What | Math | Result | So… |
|---|---|---|---|
| Payments/s | 10M ÷ 10⁵ | **100/s avg, 300/s peak** | Small; one SQL primary handles it. Correctness is the problem, not volume |
| Processor calls/s | 300 × 3 | **~1k/s peak** | Under the processor's limit on a normal day |
| Refunds/s | 2% × 100/s | **~2/s** | Tiny |
| Storage | 10M × ~3 KB (row + history) | **~30 GB/day → ~11 TB/yr; ×7 yrs** | Partition by month; archive old months to cheap storage |
| Flash sale | 5k/s for 10 min | **3M payments; 2.5× the processor limit** | **Queue** and drain at ~2k/s: backlog clears ~15 min after the sale |
| Reconciliation | 10M rows/day vs. a 10M-line report | **One batch job, minutes** | Runs nightly on a replica |

**What the numbers tell you:** on a normal day this is a small system; the hard part is **correctness under failure**. The flash sale is the one time volume matters, and only because the **processor** is the bottleneck: we must not hammer it with retries.

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /payments` | `{ orderId, amount, currency, paymentMethodToken, capture: auto\|manual }` + `Idempotency-Key` | `201 { paymentId, status: authorized\|captured }` · `402` declined · `202 { status: pending }` (flash sale / unknown outcome) · `409` key reused with a different body | Same key + same body → the **stored** response |
| `POST /payments/{id}/capture` | `{ amount? }` + `Idempotency-Key` | `200 { status: captured }` · `409` (not authorized) | Partial capture allowed |
| `POST /payments/{id}/void` | — | `200 { status: voided }` | Cancel an authorization |
| `POST /payments/{id}/refunds` | `{ amount, reason }` + `Idempotency-Key` | `201 { refundId, status: pending }` · `422` (more than refundable) | Result arrives by webhook |
| `GET /payments/{id}` | — | `200 { status, amount, captured, refunds[], history[] }` | — |
| `POST /webhooks/psp` | processor event (signed) | `200` | Verified + deduped by `event_id` |
| `GET /reconciliation/{date}` (ops) | — | `200 { matched, mismatches[] }` | — |

**Payment states:**
```
created ──authorize──► authorized ──capture──► captured ──refund(s)──► partially_refunded / refunded
   │                      └──void──► voided
   ├──declined──► failed
   └──timeout──► unknown ──recovery worker asks the processor──► authorized | failed
```

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| Payment Service | Charge, capture, void, refund; owns the state machine | **Azure SQL**: `payments` (idempotency_key UNIQUE, status, version), `payment_history` (append-only), `refunds` (idempotency_key UNIQUE), outbox |
| Webhook Handler | Async results from the processor (3-D Secure, refunds, disputes) | `processed_events` (event_id UNIQUE) |
| Recovery worker | Resolves payments stuck in `unknown` by **querying** the processor | Runs every 30 s |
| Reconciliation job | Daily: settlement report vs. our rows | Report → **Blob**; mismatches → SQL + alert |
| Kafka | `payment-events` to the Order Service (and, in a flash sale, `payment-requests`) | Azure Event Hubs (Kafka API) |
| Card processor | Holds cards, talks to banks | External (Stripe / Adyen), idempotency keys supported |

**Write path (charge):**
1. Order Service → `POST /payments` with an `Idempotency-Key`.
2. Insert `payments(status = created)`; the key is **UNIQUE**, so a retry finds the existing row and gets the stored result instead of a second payment.
3. Call the processor's authorize (and capture, if `capture = auto`) with **our `paymentId` as its idempotency key**, so even if *we* retry the call, the processor charges once.
4. Write the result (`authorized` / `failed`) + a history row + outbox in one transaction → Kafka `payment-events` → Order Service.
5. **Timeout:** we don't know if the card was charged. Mark `unknown`, return `202 pending`. The **recovery worker** asks the processor ("what happened to paymentId X?") and resolves it. **Never retry a charge blindly after a timeout.**

**Capture / void:** `UPDATE payments SET status = 'capturing' WHERE id = @id AND status = 'authorized'`; only one request can win, so a double-clicked capture can't capture twice. Then call the processor.

**Refund:** in one transaction, lock the payment row, check `sum(refunds) + amount ≤ captured`, insert the refund (key UNIQUE) → call the processor → the result arrives by webhook.

**Webhooks:** verify the signature → insert `event_id` (UNIQUE; duplicates stop here) → apply the status change with the same conditional `UPDATE` → outbox.

**Reconciliation (daily 02:00):** fetch the processor's settlement report → Blob → match each line to our payment / refund by the processor reference → write mismatches (missing on our side, missing on theirs, amount or status differs) → alert ops. It's the safety net for every bug the layers above didn't catch.

**Read path:** `GET /payments/{id}` from the **SQL primary** (a replica could show a stale "pending" right after a change); the ops dashboard reads the mismatches.

## Flash sale (row B): what changes, in order
1. `POST /payments` returns **`202 pending`** right away instead of waiting on the processor.
2. Dedupe: **a)** `SET key NX` in Redis (fast in-flight check at 5k/s), **b)** insert into Payments (key UNIQUE, still the real guard) + outbox. Payments are **sharded by paymentId** for write throughput.
3. Outbox → Kafka `payment-requests` (partitioned by paymentId).
4. **Payment workers** take requests and ask the **rate limiter** (token bucket in Redis, ~2k calls/s per processor account) for a token; a **circuit breaker** pauses calls when the processor errors, and the queue simply waits: no retry storm.
5. Call the processor (key = paymentId). Retries only on `429` / `5xx`, with exponential backoff + jitter; a **timeout** goes to `unknown` → the recovery worker (as in A).
6. Result → Payments + outbox → Kafka → Order Service. The shopper sees "processing" for a few seconds, then the result.
- Capture runs **in batches** after the sale; refunds, webhooks and reconciliation are as in A.

## Which components each path touches
| Requirement | Path | Normal checkout (row A) | Flash sale (row B) |
|---|---|---|---|
| Charge | Write | Payment Service → Payments (key UNIQUE) → processor (key = paymentId) → status + outbox → Kafka → Order Service | `202` → Redis NX + Payments → Kafka `payment-requests` → workers → rate limiter → circuit breaker → processor → status + outbox |
| Never charge twice | All | Unique keys, processor idempotency key, conditional UPDATEs, timeouts queried not retried, webhook dedupe | Same + Redis NX + **no retry storms** (queue, token bucket, circuit breaker) |
| Capture / void | Write | Conditional UPDATE (`WHERE status = 'authorized'`) → processor | Same, in batches after the sale |
| Refund | Write | Refunds (key UNIQUE, sum ≤ captured) → processor → webhook | Same |
| Reconcile | Batch | Settlement report → Blob → match → mismatches + alert | Same (bigger day) |
| Status | Read | SQL primary | Same; "pending" for longer |

## Deep dives to drive (learner leads, interviewer pushes)
1. **Idempotency keys end to end:** who generates the key (the caller, per checkout attempt), what's stored with it (request hash + response), how long it's kept (e.g. 24 h–7 days), and what happens if the same key arrives with a different amount (`409`).
2. **The timeout problem:** the processor call times out. Did the charge happen? Why retrying is dangerous, why querying is safe, and why the processor's idempotency key makes even a retry safe *if* you reuse the same key.
3. **Authorize vs. capture:** why split them (ship-then-charge, release unused holds). Authorizations expire after ~7 days; what if the order ships on day 8?
4. **Exactly-once is a lie:** where each hop is at-least-once (caller retries, Kafka redelivery, webhook redelivery) and which dedupe stops each one.
5. **Outbox:** why the status change and the `payment-events` message must be written in one transaction (M7/M8): a crash between them would tell the Order Service nothing, or something false.
6. **Reconciliation mismatch types:** charged by the processor but `failed` on our side (refund or fix), `captured` on our side but missing in the report (timing or a real bug), amount differences (partial captures, currency rounding).
7. **Processor outage:** circuit breaker open → queue grows → shoppers see "pending" → optional second processor for failover (now you need routing and two reconciliations).

## Interview follow-ups (answer these after the design)
- The Order Service retries `POST /payments` three times because of network errors. Walk through what happens at each layer.
- The authorize call times out, and the shopper hits "Pay" again with a new idempotency key. Could they be charged twice? How do you prevent it?
- Reconciliation finds a payment the processor captured but we marked `failed`. What do you do, and how did it happen?
