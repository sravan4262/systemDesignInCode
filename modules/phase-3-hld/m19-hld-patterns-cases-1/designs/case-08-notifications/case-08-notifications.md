# Case 8: Notification system

Diagram: [case-08-notifications.excalidraw](case-08-notifications.excalidraw), in two rows: **A. Transactional** (every path) and **B. Broadcast campaign** (50M users; same layout, only what differs, numbered in order with the event count on each hop). Callers → APIM (tall box) → a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design a notification system that other services use to notify users."

## Functional requirements (in scope; agreed 2026-11-21)
1. **Send a notification** to a user on one or more **channels**: mobile push, SMS, email, in-app inbox.
2. **Respect user preferences:** per-channel opt-outs, unsubscribe, **quiet hours** (no marketing push at 3 AM).
3. **Templates:** a caller sends `{ template: "order_shipped", userId, data }`; the system renders the text per channel and language.
4. **Reliable delivery:** retries when a provider fails, **no duplicates**, delivery status tracked (sent → delivered / failed).
5. **Rate limits:** per user (e.g. ≤ 5 marketing pushes a day) and per provider (stay under APNs / Twilio / email limits).
6. **Status tracking:** for every notification and campaign, what succeeded, failed or was skipped, per user and channel, with the reason; campaign progress and totals; re-send only the failures.

**Out of scope (say this out loud):** **priority lanes** (one topic for all notifications; see deep dive 1), scheduled sends ("9 AM in each user's time zone"), building the providers (we call **APNs / FCM** for push, **Twilio** for SMS, **SendGrid / Azure Communication Services** for email), A/B tests of message text, analytics dashboards, user segmentation itself (marketing gives us a segment).

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Latency (transactional) | p99 < 5 s from API call to provider; OTP < 10 s on the phone | An OTP that arrives late is useless |
| Latency (campaign) | 50M users within ~30–60 min | Nobody needs a sale push in seconds |
| Delivery semantics | **At least once + dedupe** → no duplicate a user can see | Exactly-once isn't possible across providers |
| Campaign impact | A campaign may slow other sends a little, never stop them | The campaign throttle caps its share (no priority lanes) |
| Compliance | Opt-outs and unsubscribes honoured always, immediately | Legal (spam / SMS consent laws), and trust |
| Availability | Accepting notifications always works (AP); sending can lag | Callers shouldn't fail because a provider is slow |

## Capacity estimate (high level)
**Facts:** 100M DAU; ~5 transactional notifications per user per day, ~1.5 channels each; 10M OTP SMS/day; one 50M-user campaign a day; peak = 3× average; 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Transactional notifications/s | 500M ÷ 10⁵ | **5k/s avg, 15k/s peak** | Kafka + a small router fleet |
| Provider sends/s | 15k × 1.5 channels | **~22k/s peak** | One worker pool per channel; per-provider token buckets |
| OTP SMS/s | 10M ÷ 10⁵ | **~100/s avg, 300/s peak** | Small but **costly and urgent** |
| Campaign | 50M in 30 min | **~28k sends/s for 30 min** | Doubles the load: needs a **send-rate cap** |
| Delivery rows | (500M + 50M) × ~1.5 × 300 B | **~250 GB/day** | Cosmos with a 30–90 day TTL |

**What the numbers tell you:** steady transactional traffic is moderate; a single campaign **doubles** it in one burst. The design is about **politeness** (a campaign send-rate cap, provider and user rate limits), not raw throughput.

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /notifications` | `{ userId, template, data, channels?, category: transactional\|marketing }` + `Idempotency-Key` | `202 { notificationId }` · `409` key reused with a different body | Accept fast; send async |
| `GET /notifications/{id}` | — | `200 { channels: [{ channel, status, at }] }` | sent / delivered / failed / skipped (opt-out) |
| `POST /campaigns` | `{ segmentId, template, data, channels }` | `202 { campaignId }` | Marketing tool only |
| `GET /campaigns/{id}` · `POST /campaigns/{id}/cancel` | — | `200 { status, rangesDone, rangesTotal, perChannel: { sent, delivered, failed, skipped } }` | Cancel = kill switch |
| `GET /campaigns/{id}/deliveries?status=failed&channel=&cursor=` | — | `200 { items: [{ userId, channel, reason, attempts }], nextCursor }` | Who failed and why |
| `POST /campaigns/{id}/retry-failed` | — | `202` | Re-queues only failed deliveries; safe because of the unique row |
| `PUT /users/{id}/preferences` | `{ channels: { push, sms, email, inApp }, quietHours, marketing: bool }` | `204` | User app |
| `POST /devices` | `{ platform: ios\|android, pushToken }` | `204` | Registered on app start |
| `GET /users/{id}/inbox?cursor=` | — | `200 { items[], nextCursor }` | In-app inbox |
| `POST /callbacks/{provider}` | provider receipt (signed) | `200` | Delivered / bounced / invalid token |

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| Notification API | Accepts, dedupes (key UNIQUE), writes + outbox | **Notifications (SQL)** + outbox → Kafka |
| Kafka topic `notifications` | Every accepted notification (transactional and campaign batches) | Partitioned by userId |
| Router | 1) preferences, opt-outs, quiet hours; 2) frequency caps; 3) render the template per channel + language; then fan out per channel | Prefs, devices, templates: **Redis cache → SQL**; caps: Redis counters |
| Kafka by channel | `push`, `sms`, `email`, `in-app` | One worker pool per channel scales on its own |
| Channel workers | Call the provider; token bucket per provider; retry with backoff; dead-letter after N tries | APNs / FCM, Twilio, SendGrid / ACS; in-app → **Inbox (Cosmos, pk userId)** + WebSocket push if online |
| Deliveries | Status per (notification or campaign, user, channel): pending → sent → delivered / failed / bounced, or skipped, with reason and attempts; duplicates stop here | **Cosmos**, unique per (id, user, channel); pk `(campaignId, range)` for campaigns (one campaign would overflow a single partition); TTL 90 days |
| Callback Handler | Provider receipts: delivered, bounced, invalid push token (→ delete the device) | Updates Deliveries / devices |
| Campaign Service + range producer / range consumer (row B) | Range producer asks the warehouse for N ranges → 1 event per range; range consumers page their range → 1 event per 1,000 users on `notifications` | Campaigns (SQL: status, kill switch, ranges, totals), **data warehouse** (segment members), progress counters in **Redis** |

**Write path (transactional):**
1. `POST /notifications` + `Idempotency-Key` → insert (key UNIQUE; a retry returns the same `notificationId`) + outbox → Kafka topic **`notifications`** → `202`.
2. **Router** (consumer group on `notifications`):
   1. load preferences and devices: channel opted out → `skipped`; quiet hours → defer **marketing**, send **transactional** (an OTP ignores quiet hours: the user asked for it);
   2. frequency cap: `INCR cap:{user}:{day}` for marketing; over the cap → `skipped`;
   3. render the template per channel and language.
3. One message per channel → Kafka **by channel** → channel worker:
   - insert into Deliveries `(notificationId, channel)` first; if it already exists, it was sent → stop (dedupe on redelivery);
   - take a token from the provider's bucket; call the provider (with an idempotency key where supported);
   - `5xx` / `429` → retry with backoff + jitter; after N tries → dead-letter queue + `failed`.
4. **Receipts:** providers call back → Deliveries `delivered` / `bounced`; an invalid push token deletes that device.

**Read path:** status per channel from Deliveries; the in-app inbox from Cosmos by `userId` (newest first, cursor pages).

## Broadcast campaign (row B): what changes, in order
1. **Campaign Service:** `POST /campaigns` → Campaigns (SQL) row `c-77 | segment | template | status = running` → `202`.
2. **Range producer:** asks the **data warehouse** to cut the segment into **N ranges** with about equal members (N ≈ members ÷ 50k; one aggregate query that returns only boundaries, e.g. `NTILE(N)`), and records N range rows `pending` in Campaigns.
3. Kafka topic **`campaign-ranges`**: **1 event per range** `{ c-77, range 37, fromId, toId }`, keyed by campaign + range so they spread over partitions.
4. **Range consumer** (consumer group): for each range event: kill switch? → page the warehouse `userId BETWEEN fromId AND toId`, 1,000 at a time → **publish 1 event per page** → mark the range `done` → ack. A crash re-runs only that range.
5. Kafka topic **`notifications`**: **1 event per 1,000 users** `{ c-77, userIds[1000] }` (the same topic as row A).
6. **Router** (consumer group, same as row A): for **each user** in the batch: kill switch, current preferences, quiet hours, marketing cap, render in their language → **1 event per user per allowed channel**: up to 1,000 events per channel per batch, each with its own text and address. Skipped users → Deliveries `skipped` with the reason.
7. Kafka topics **`push` / `email` / `sms`** (1 event per user).
8. **Push / Email / SMS workers** (one consumer group per channel): Deliveries row first (unique → no duplicates), campaign + provider token buckets, call the provider (grouped where it allows: FCM 500 tokens, SendGrid 1,000 recipients, Twilio 1 per call), retry with backoff, dead-letter after N.
9. **Status:** workers write `sent` / `failed`, provider callbacks write `delivered` / `bounced` (our delivery id is passed to the provider, so callbacks map straight back).

**Event counts:** 1 campaign → N range events → members ÷ 1,000 batch events → up to 1,000 events per channel per batch (one per user).

**Status at three levels:**
| Level | Where | Answers |
|---|---|---|
| Delivery (user + channel) | **Deliveries (Cosmos)**, pk `(campaignId, range)` | Did this user get it on this channel? If not, why? |
| Range | **Campaigns (SQL)** range rows: pending / running / done + counts | What's finished, what to resume |
| Campaign | Live counters in **Redis** (bumped only when a delivery row actually changes state, so retries and duplicate callbacks don't inflate them); final totals written to **Campaigns (SQL)** when all ranges are done | Progress, totals per channel |

Status only moves forward (a conditional update: a late `sent` never overwrites `delivered`). `POST /campaigns/{id}/retry-failed` re-queues only the failed rows.

**Why ranges + the warehouse:** sending starts in seconds (no 50M-row export), the work is parallel (add range consumers), nothing large is stored, resume / cancel work per range, and campaign reads never touch the live user databases. The warehouse may be minutes behind; the router checks each user's **current** preferences at send time.
- Transactional sends share the same `notifications` topic and workers. The campaign throttle keeps the campaign from taking all the capacity; an OTP sent mid-campaign can still wait behind batches already queued (seconds with the cap).

## Which components each path touches
| Requirement | Path | Transactional (row A) | Campaign (row B) |
|---|---|---|---|
| Send | Write | API (key UNIQUE) + outbox → Kafka `notifications` → router → Kafka by channel → worker → provider | Campaign Service → range producer (warehouse: N ranges) → `campaign-ranges` (1 per range) → range consumer (pages warehouse) → `notifications` (1 per 1,000 users) → router (1 per user per channel) → `push` / `email` / `sms` → channel workers |
| Preferences + quiet hours | Write | Router, per notification | Router, per user in each batch |
| No duplicates | Write | Idempotency key + Deliveries `(notification, channel)` UNIQUE | `(campaign, user)` UNIQUE |
| Rate limits | Write | Per-user caps (Redis) + per-provider token buckets | + campaign send-rate cap |
| Status | Read | Deliveries by notificationId | Progress (Redis counters + ranges done) and failed deliveries by (campaign, range) |
| In-app inbox | Read | Inbox (Cosmos, pk userId) | Same |

## Deep dives to drive (learner leads, interviewer pushes)
1. **Adding priorities later (out of scope now):** with one topic, a 50M-user campaign can put an OTP behind its batches (head-of-line blocking). The fix: separate topics (`notify-high` / `notify-low`) *and* separate consumer groups / worker pools. Why a "priority" field on one queue isn't enough.
2. **Duplicates:** where each one comes from (caller retry, Kafka redelivery, worker crash after the provider call) and which check stops it. Which duplicates are unavoidable? (A crash after the provider accepted but before we recorded it.)
3. **Provider failure:** Twilio is down. Circuit breaker → queue builds → fail over to a second SMS provider for OTPs only? Cost vs. reliability.
4. **Quiet hours and time zones:** store the user's time zone; deferred messages go to a delay queue (or a "send-after" timestamp) instead of blocking the router.
5. **Push tokens:** tokens change and expire; providers report invalid tokens → delete them, or every campaign wastes calls on dead devices.
6. **Templates:** versioned, per language with a fallback; rendering in the router (once per channel) vs. in the worker.
7. **Campaign kill switch:** someone sent the wrong price to 50M users. How fast can you stop it, and how do you know who already got it?

## Interview follow-ups (answer these after the design)
- A 50M-user campaign starts at 10:00, and a user requests an OTP at 10:01. How long can the OTP wait in this design, and what would you change to guarantee seconds?
- The push worker crashes right after APNs accepted the push, before it recorded `sent`. What happens when the message is redelivered?
- A user turns off marketing emails at 10:00:05 while a campaign is mid-flight. Will they get the email?
