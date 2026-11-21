# Case 8: Notification system

Diagram: [case-08-notifications.excalidraw](case-08-notifications.excalidraw), in two rows: **A. Transactional** (every path) and **B. Broadcast campaign** (50M users; same layout, only what differs, numbered in order). Callers → APIM (tall box) → a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design a notification system that other services use to notify users."

## Functional requirements (in scope; agreed 2026-11-21)
1. **Send a notification** to a user on one or more **channels**: mobile push, SMS, email, in-app inbox.
2. **Respect user preferences:** per-channel opt-outs, unsubscribe, **quiet hours** (no marketing push at 3 AM).
3. **Templates:** a caller sends `{ template: "order_shipped", userId, data }`; the system renders the text per channel and language.
4. **Reliable delivery:** retries when a provider fails, **no duplicates**, delivery status tracked (sent → delivered / failed).
5. **Rate limits:** per user (e.g. ≤ 5 marketing pushes a day) and per provider (stay under APNs / Twilio / email limits).

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
| `GET /campaigns/{id}` · `POST /campaigns/{id}/cancel` | — | `200 { sent, failed, skipped, status }` | Cancel = kill switch |
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
| Deliveries | Status per (notification, channel); duplicates stop here | **Cosmos**, `(notificationId, channel)` unique, TTL 90 days |
| Callback Handler | Provider receipts: delivered, bounced, invalid push token (→ delete the device) | Updates Deliveries / devices |
| Campaign Service (row B) | Splits the audience into userId **ranges**; task workers read their range of the segment and publish 1k-user batches on `notifications`; throttle; kill switch | Campaigns (SQL: kill switch, status per range), segment members from the **data warehouse**, progress in Redis |

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
1. `POST /campaigns` → `202`; Campaigns row (segment, template, status).
2. **Producer:** asks the **data warehouse** for **1,000 range boundaries** with roughly equal numbers of segment members (one cheap aggregate query, no userIds returned), records each range as `pending` in Campaigns, and publishes one tiny task per range (`{ campaignId, range }`). Nothing is exported: no 50M-row list, no files.
3. Kafka topic **`campaign-tasks`** (1,000 messages, a few KB in total).
4. **Consumer group: task workers**, one range at a time: page through "segment members in my range" from the **data warehouse** (`WHERE userId >= @from AND userId < @to ORDER BY userId`, keyset pages of 1k). Each range's status is recorded, so a crash re-runs only that range.
5. Each page → one **1k-user batch** on Kafka topic **`notifications`** (the same topic as row A).
6. **Consumer group: router** (the same router as row A): per user, preferences (always the *current* ones), quiet hours, caps, and the **kill switch** (campaign cancelled → drop).
7. **Campaign throttle:** a send-rate cap (~30k/s) plus the provider token buckets.
8. **Consumer group: push / email workers** (the same channel workers as row A); Deliveries `(campaignId, userId)` UNIQUE, so a re-run range never sends twice.

**Why ranges instead of exporting the segment:** sending starts in seconds (no waiting for a 50M-row export), the work is parallel (add task workers to go faster), nothing large is stored, and resume / cancel work per range. **Why the warehouse:** segments ("US users who bought in the last 90 days") are defined there anyway, it's built for big scans, and it's a **separate copy** fed by CDC, so campaign reads never touch the live user databases. It may be minutes to hours behind; that's fine, because the router checks each user's **current** preferences at send time. *Alternative if the warehouse is too slow for this:* a segment job writes the members into a Redis bitmap (1 bit per userId, 12.5 MB per segment) and task workers read their slice of bits.
- Transactional sends share the same topic and workers. The **campaign throttle** keeps the campaign from using all the capacity, but an OTP sent mid-campaign can still wait behind campaign batches already in the topic (seconds, not minutes, with the cap). Progress (sent, failed, skipped) is counted in Redis.

## Which components each path touches
| Requirement | Path | Transactional (row A) | Campaign (row B) |
|---|---|---|---|
| Send | Write | API (key UNIQUE) + outbox → Kafka `notifications` → router → Kafka by channel → worker → provider | Campaign Service → producer (1,000 range tasks) → topic `campaign-tasks` → consumer group: task workers (page their range from the warehouse, 1k at a time) → topic `notifications` → consumer group: router → throttle → consumer group: channel workers |
| Preferences + quiet hours | Write | Router, per notification | Router, per user in each batch |
| No duplicates | Write | Idempotency key + Deliveries `(notification, channel)` UNIQUE | `(campaign, user)` UNIQUE |
| Rate limits | Write | Per-user caps (Redis) + per-provider token buckets | + campaign send-rate cap |
| Status | Read | Deliveries by notificationId | Progress counters (Redis) |
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
