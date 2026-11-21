# Case 6: Ticketmaster (event tickets)

Diagram: [case-06-ticketmaster.excalidraw](case-06-ticketmaster.excalidraw), in two rows: **A. Regular event** (every path) and **B. Hot on-sale** (1M fans at 10:00:00 for 50k seats; only what differs from A). User → APIM (tall box) → a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design Ticketmaster. Users can find events and buy tickets."

## Functional requirements (in scope; learner's list, 2026-11-21)
1. **Search events** by name, city and date.
2. **View an event:** details, prices and a **seat map** showing which seats are free.
3. **Select seats and hold them:** exact seats ("Row F, seat 12"); they're reserved for **10 minutes** while the user pays. Max **4 seats per user** per event.
4. **Make a payment** for the held seats.
5. **Receive a confirmation:** order page + email with the tickets.

**Row B has the same requirements**, plus a **virtual waiting room** in front of booking for hot on-sales.

**Out of scope (say this out loud):** creating events (organisers do it elsewhere), resale and transfers, refunds, dynamic pricing, QR codes and scanning at the venue, "best available" auto-seating.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| **No double-selling** | A seat is sold to at most one person, always (CP for the sale) | The defining bug of this system |
| Hold correctness | A held seat can't be held by anyone else until it's bought, released or expires | Otherwise users lose seats mid-payment |
| Search / browse | Available (AP), p99 < 300 ms; a seat map a few seconds stale is fine | The hold is the real check |
| Fairness (hot) | First come, first served through the waiting room; bots limited | Users and artists judge the system by this |
| Payment | Charged once (idempotency key); never charged without a ticket | authorize → confirm → capture, or void |
| Scale (hot) | 1M users arrive in one minute; the system degrades gracefully, never falls over | One on-sale must not take down the rest of the site |

## Capacity estimate (high level)
**Facts:** 20M DAU browsing; 5 searches and 10 event views per user per day; 2M tickets sold/day; peak = 3× average; 1 day ≈ 10⁵ s. Hot on-sale: 1M users at 10:00:00, 50k seats, sells out in ~15 min.

| What | Math | Result | So… |
|---|---|---|---|
| Searches/s | 100M ÷ 10⁵ | **1k/s avg, 3k/s peak** | Search index with replicas |
| Event / seat-map views/s | 200M ÷ 10⁵ | **2k/s avg, 6k/s peak** | Cache event details; seat map from Redis + SQL |
| Tickets sold/s (normal) | 2M ÷ 10⁵ | **~20/s** | SQL handles this easily |
| Hot on-sale, no queue | 1M users refreshing the seat map every 5 s | **~200k req/s on ONE event** | Impossible to serve fairly → **waiting room** + 2 s seat-map snapshot |
| Hot on-sale, with queue | 50k seats ÷ 15 min | **~55 tickets/s; admit ~5k users / min** | The booking path sees a steady, small load |
| Storage | 1M events × 50k seats × ~100 B | **~5 TB of seats; orders ~1 TB/yr** | Small; SQL sharded by event |

**What the numbers tell you:** on a normal day this is a small system. The whole design problem is **one hot event**: 1M people want 50k seats in the same minute. The answer isn't more servers for booking; it's **controlling who gets to book** (waiting room) and making the seat itself the unit of contention (holds + a unique constraint).

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `GET /events?q=&city=&from=&to=&cursor=` | — | `200 { events[], nextCursor }` | Search |
| `GET /events/{id}` | — | `200 { id, name, venue, startsAt, priceTiers[] }` | Cached |
| `GET /events/{id}/seats` | — | `200 { seats: [{ seatId, section, row, number, price, status: free\|held\|sold }], asOf }` | May be ~2 s stale |
| `POST /events/{id}/holds` | `{ seatIds[] }` + `Idempotency-Key` | `201 { holdId, expiresAt }` · `409 { unavailableSeatIds[] }` · `429` (over 4 seats) | **All seats or none** |
| `DELETE /holds/{holdId}` | — | `204` | Release early |
| `POST /holds/{holdId}/checkout` | `{ paymentToken }` + `Idempotency-Key` | `202 { orderId, status: pending }` · `410` (hold expired) | Card details are tokenised by the processor's widget |
| `GET /orders/{id}` | — | `200 { status: pending\|confirmed\|failed, tickets[] }` | Confirmation page |
| **Hot only:** `POST /events/{id}/queue` | — | `200 { queueToken, position }` | Join the waiting room |
| **Hot only:** `GET /queue/{queueToken}` | — | `200 { position, etaSec, accessToken? }` | Poll every ~10 s; `accessToken` once admitted |

- In a hot on-sale, `POST /holds` and `POST /checkout` require the **access token** (checked at APIM); without it → `403`.

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| Search Service | Event search | **Azure AI Search**, fed by CDC from the Events DB |
| Event Service | Event page and seat map | **Redis** cache → **Events (SQL)**; seat map = held (Redis) + sold (SQL) |
| Booking Service | Holds, checkout, orders | **Seat holds: Redis** (temporary, TTL); **Orders + Tickets: Azure SQL**, sharded by `eventId` |
| Payment Service | Authorize, capture, void | Card processor (Stripe / Adyen) with an idempotency key |
| Notification Service | Confirmation email and in-app message | Kafka consumer of `order-confirmed` |
| Waiting Room Service (hot) | Queue position, admission, access tokens | **Redis** sorted set per event; static waiting page on the **CDN** |

**The key split: holds are temporary, sales are permanent.**
- A **hold** lives 10 minutes and is fine to lose (the user just picks again), so it lives in **Redis** with a TTL: fast, and it expires on its own.
- A **sale** must never be lost or duplicated, so it lives in **SQL** with `UNIQUE (event_id, seat_id)` on the tickets table. Even if Redis misbehaved, the database can't sell a seat twice.

**Write path:**
1. **Hold:** `POST /holds {seatIds}` → Booking Service runs one **Lua script** in Redis: if *every* seat key is free, `SET seat:{event}:{seat} holdId NX EX 600` for all of them; otherwise set none and return the taken seats (`409`). All-or-nothing, atomic, no partial holds.
2. **Checkout:** `POST /holds/{id}/checkout` → check the hold still belongs to this user (and extend it to cover payment) → insert `orders(pending)`.
3. **Pay, then confirm, then capture:**
   1. Payment Service **authorizes** the card (money reserved, not taken).
   2. One SQL transaction: `INSERT tickets (event, seat, order)` for each seat (the **unique constraint** is the final guard) + `orders → confirmed` + outbox row.
   3. **Capture** the payment. If step 2 failed (a seat was somehow sold, or the hold expired and was re-sold), **void** the authorization instead and tell the user: they're never charged without a ticket.
4. Delete the hold keys. Outbox → Kafka `order-confirmed` → Notification Service → email + in-app.
5. **Expiry:** if the user never pays, the Redis TTL frees the seats after 10 minutes; nothing to clean up.

**Read path:**
1. **Search:** Search Service → AI Search (updated by CDC from Events).
2. **Event page:** Event Service → Redis → Events (SQL) on a miss.
3. **Seat map:** sold seats (SQL) + held seats (Redis) → free / held / sold.
4. **Confirmation:** `GET /orders/{id}` → SQL read replica (falls back to the primary right after checkout for read-your-writes).

## Hot on-sale (row B): what changes
1. **Virtual waiting room.** At 10:00 everyone calls `POST /events/{id}/queue` and gets a **queue token** + position (`ZADD queue:{event} <arrival time> user`). The waiting page is static and served from the CDN; the app polls `GET /queue/{token}` every ~10 s (`ZRANK` → position).
2. **Admission control.** Every 10 s the admission controller lets in the next **K** users, where K = how many the booking path can serve (≈ seats still free and checkout capacity). Each admitted user gets a **signed access token** (eventId, userId, 15 min). APIM rejects booking calls without one.
3. **Per-user limit + dedicated shard.** `POST /holds` first checks `user:{event}:{user} ≤ 4` (Redis), then runs the hold script on a **Redis shard dedicated to this event**, so one hot event can't slow down holds for everything else.
4. **Seat map snapshot.** Admitted users load the seat map from a **snapshot rebuilt every 2 s** (Redis + CDN), not live per request. Picking a seat that was taken in the last 2 s just returns `409` → pick another.
5. **Sold out:** when free seats hit 0, the admission controller stops, and everyone still waiting is told immediately instead of waiting for nothing.

## Which components each path touches
| Requirement | Path | Regular event (row A) | Hot on-sale (row B) |
|---|---|---|---|
| Search events | Read | Search Service → AI Search | Same |
| View event | Read | Event Service → Redis → Events (SQL) on a miss | Same (event page also CDN-cached) |
| Seat map | Read | Holds (Redis) + sold (SQL), live | **Snapshot (CDN + Redis), rebuilt every 2 s** |
| Get in | Write | — | **Waiting room:** queue (Redis) → admission controller → access token |
| Hold seats | Write | Booking Service → Redis Lua (all `NX`, 10 min TTL) | Access token checked at APIM → per-user limit (≤ 4) → **dedicated Redis shard** |
| Pay | Write | Orders (pending) → authorize → Tickets INSERT (unique) → capture / void | Same |
| Confirmation | Write + read | Outbox → Kafka → Notification Service → email; `GET /orders/{id}` | Same |

**What never protects against double-selling on its own:** the seat map (stale by design) and the Redis hold (temporary). **What does:** `UNIQUE (event_id, seat_id)` in SQL.

## Deep dives to drive (learner leads, interviewer pushes)
1. **Where do holds live?** Redis with TTL (fast, expires itself) vs. a SQL `seats` row with `held_until` (one store, but you need a sweeper and row locks under contention). What happens if the Redis node holding the holds fails over and loses the last second of writes? (Two users may both hold a seat; the unique constraint at confirm picks one, the other is voided.)
2. **Hold expires during payment:** the card authorization takes 30 s and the hold expires at second 20, and someone else holds the seat. Extend the hold at checkout start; at confirm, the unique insert decides; the loser is voided, never charged.
3. **authorize → confirm → capture:** why not charge first and then write tickets? (A crash between the two = charged without a ticket.) Why not write tickets first and then charge? (A failed charge = a seat blocked by an unpaid ticket.)
4. **Waiting-room fairness:** people who opened the page at 9:55 vs. 10:00:00. Common trick: everyone who arrives before the start gets a **random** position; after the start, arrival order. Bots: one queue token per account + device checks + CAPTCHA at join.
5. **Sizing K:** admit too many and the seat map turns into a stampede of `409`s; too few and seats sit unsold. Tie K to free seats and checkout throughput; re-evaluate every 10 s.
6. **Hot shard:** one event's holds on one Redis shard: ~55 holds/s plus reads is nothing for Redis; the shard is dedicated for **isolation**, not throughput.
7. **Search freshness:** CDC lag means a just-sold-out event may still show "tickets available" for a few seconds; the event page and seat map are the truth.

## Interview follow-ups (answer these after the design)
- Two users click "hold" on seat F12 at the same millisecond. Walk through exactly what each sees.
- The payment is authorized, then the Booking Service crashes before writing the tickets. What happens to the user's money and to the seats?
- Why not just autoscale the booking service to 1M users instead of building a waiting room?
