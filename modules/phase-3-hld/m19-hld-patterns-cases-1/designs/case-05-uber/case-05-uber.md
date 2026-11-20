# Case 5: Uber (ride hailing)

Diagram: [case-05-uber.excalidraw](case-05-uber.excalidraw), in two rows: **A. Normal ride** and **B. Hot spot** (a stadium lets out: 50k requests in 15 minutes, 2k drivers nearby, surge pricing). Both rows have the same requirements. Two clients (driver app, rider app) → one tall gateway (APIM for REST + a WebSocket gateway) → a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design Uber. Riders request a ride, and a nearby driver picks them up."

## Functional requirements (in scope; learner's list, 2026-11-20)
1. **Fare estimate:** the rider enters pickup and dropoff and sees a price. The price is **locked** for ~2 minutes and is what they pay.
2. **Request a ride:** the system finds the **nearest available drivers**.
3. **Assignment:** the nearest driver gets an **offer** with 10 s to accept, else the next driver is tried. **One driver per ride, one ride per driver.**
4. **Live driver location:** once assigned, the rider sees the driver moving on the map until the ride ends.
5. **Payment at the end of the ride:** when the driver taps "end", the rider's card on file is charged the locked price.

**Supporting flows (needed, but not features):** drivers send their location every ~4 s (for 2 and 4); the driver taps accept / start / end (for 3, 4 and 5).

**Row B has the same requirements**, with surge: in a hot cell the estimate includes a surge multiplier.

**Out of scope (say this out loud):** trip history, ratings, cancellations, pooled and scheduled rides, metered fares, tips, adding a card (assume one is on file), driver payouts, maps and routing (an external Maps API gives routes and ETAs).

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Matching time | A driver offer within ~5 s of the request | Riders give up after ~1 minute |
| Location freshness | The rider's map lags the driver by ≤ ~5 s | Older is useless at pickup |
| Assignment consistency | One driver per ride, one ride per driver, always (CP for this step) | Double-booking a driver is the worst bug here |
| Payment correctness | Charged **exactly once in effect**: at-least-once delivery + an idempotency key | A double charge is a support ticket and a refund |
| Availability | Location ingest and tracking keep working (AP) | A dropped ping is fine; the next one comes in 4 s |
| Geography | Global, but every ride is **local to one city** | Shard by city / region |

## Capacity estimate (high level)
**Facts:** 25M rides/day; ~1M drivers online at peak, pinging every 4 s; average trip 20 min; peak = 3× average; 1 day ≈ 10⁵ s. Hot spot: 50k requests in 15 min in a few cells.

| What | Math | Result | So… |
|---|---|---|---|
| Location pings/s | 1M ÷ 4 s | **~250k/s** | The heaviest write; keep **latest only** in Redis, never a DB row per ping |
| Latest-location memory | 1M × ~100 B | **~100 MB** | Fits in RAM; shard by city for **write throughput**, not size |
| Ride requests/s | 25M ÷ 10⁵ | **250/s avg, ~1k/s peak** | Light; SQL is fine |
| Concurrent rides | 25M × 20 min ÷ 1,440 min | **~350k avg, ~1M peak** | ~250k location pushes/s to riders |
| Payments/s | 25M ÷ 10⁵ | **250/s avg, ~1k/s peak** | Light; the problem is correctness, not volume |
| Hot spot | 50k ÷ 900 s | **~55 requests/s in a few cells** | The rate is small; the problem is **contention** (many riders, few drivers) and **price** |

**What the numbers tell you:** location pings dominate (250k/s) and are only useful for seconds, so they live in memory and are overwritten. Requests and payments are rare by comparison; their difficulty is **correctness** (no double booking, no double charge). Every ride is local to a city, so the city is the shard key.

## API
**WebSocket** `wss://rt.example.com/ws` (JWT on connect; drivers while online, riders while a ride is active)

| Direction | Frame | Payload | Notes |
|---|---|---|---|
| Driver → server | `location` | `{ lat, lng, heading, ts }` every 4 s | Fire and forget |
| Server → driver | `offer` | `{ rideId, pickup, dropoff, price, expiresInSec: 10 }` | Driver answers with `POST /rides/{id}/accept` |
| Server → rider | `ride` | `{ rideId, status, driver?, etaSec }` | Matched, arriving, started, ended, paid |
| Server → rider | `driverLocation` | `{ lat, lng, heading }` every 4 s | Only while assigned / on the ride |

**REST**

| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /fare-estimates` | `{ pickup, dropoff }` | `200 { estimateId, price, surge, expiresAt }` | Price locked ~2 min |
| `POST /rides` | `{ estimateId }` + `Idempotency-Key` | `202 { rideId, status: matching }` | A double tap creates one ride; `410` if the estimate expired |
| `GET /rides/{id}` | — | `200 { status, driver?, etaSec, price, paymentStatus }` | Fallback after a WebSocket reconnect |
| `POST /rides/{id}/accept` (driver) | — | `200` · `409` (taken, expired, or you're busy) | The **one** contested write |
| `POST /rides/{id}/start` · `/end` (driver) | `{ ts }` | `200` | `end` → `ride-ended` → payment |

There's no "pay" endpoint: the charge is triggered by `end`, server-side, with the card on file.

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| WebSocket gateway | Driver connections (and riders during a ride); registers `user → gateway node` in presence | As in Case 2 |
| Location Service | Takes pings; overwrites the latest position; publishes the ping | **Redis geo index** (`GEOADD drivers:{city}`, status alongside); **Kafka** `driver-locations` |
| Trip tracker | For a driver on a ride, pushes each ping to the rider's gateway | Kafka consumer; `driver → rideId, riderId` cached in Redis |
| Pricing Service | Estimate = route (Maps API) × rates × surge; stores the locked price | Maps API; surge multipliers in **Redis** |
| Ride Service | Create ride, accept, start, end | **Azure SQL, sharded by city**: `rides`, `drivers` (status), outbox |
| Matching worker | Nearest available drivers → rank by ETA → lock one → offer → wait 10 s → next | Kafka consumer of `ride-requested` (partitioned by city); **Redis** driver locks |
| Payment Service | On `ride-ended`, charges the locked price once | **Payments (SQL)**, `rideId` unique; card processor (Stripe / Adyen) |
| Surge (row B) | Requests vs. available drivers per H3 cell every 30 s → multiplier | Kafka consumer → Redis |

**Write path:**
1. **Locations:** driver → WebSocket gateway → Location Service → `GEOADD` (latest only; TTL 30 s, so a vanished driver drops out of search) and → Kafka `driver-locations`.
2. **Request:** rider → `POST /rides` → Ride Service inserts `rides(status = matching, price = locked estimate)` + outbox → Kafka `ride-requested` → `202`.
3. **Match:** the matching worker runs `GEOSEARCH` (2 km, 10 nearest available), ranks by ETA, takes the best and **locks** it: `SET driver:{id}:offer rideId NX EX 15`. If the lock fails, another ride is offering to that driver, so try the next. Then it pushes the offer.
4. **Accept (the contested write):** one SQL transaction on the city shard:
   `UPDATE rides SET driver_id = @d, status = 'accepted' WHERE id = @r AND status = 'matching'` and
   `UPDATE drivers SET status = 'busy' WHERE id = @d AND status = 'available'`.
   Both must change exactly 1 row, else roll back and return `409`. **The Redis lock is a fast filter; this conditional update is the truth.** No answer in 10 s → the lock expires → next driver.
5. **Live location:** the trip tracker consumes `driver-locations`; if the driver is on a ride, it looks up the rider's gateway (presence) and pushes `driverLocation`. Nothing is stored per ping.
6. **End + pay:** the driver taps end → ride `ended` + outbox → Kafka `ride-ended` → Payment Service:
   1. insert `payments(rideId UNIQUE, amount, status = pending)`; a duplicate event hits the unique key and stops here;
   2. call the card processor with **idempotency key = rideId**;
   3. mark `paid` (or `failed` → retry with backoff, then flag the rider's account). The rider gets a `ride: paid` push.

**Read path:**
1. **Fare estimate:** Pricing Service → Maps API (route, ETA) × rates (× surge in row B) → returns and stores the locked price.
2. **Ride status:** Ride Service → SQL read replica. Live changes arrive by **push**; this GET is the fallback after a reconnect.

## Which components each path touches
| Requirement | Path | Normal ride (row A) | Hot spot (row B) |
|---|---|---|---|
| Fare estimate | Read | Pricing → Maps API | Pricing → **surge multiplier** (Redis) × route (Maps API, cached per route) |
| Request + find drivers | Write | Ride Service → Rides (SQL) + outbox → Kafka → matching worker (greedy: nearest first) → `GEOSEARCH` | Same up to Kafka → **batch matcher per cell every 2 s** (N riders × M drivers, minimise total ETA); the rest wait for the next round |
| Assignment | Write | Driver lock (NX) → offer → accept = one conditional UPDATE on rides + drivers | Same (the conditional update makes simultaneous accepts safe) |
| Live driver location | Write (push) | Location Service → geo index + Kafka → trip tracker → rider's gateway | Same |
| Payment | Write | ride-ended → Payment Service → Payments (unique rideId) → card processor (idempotency key) | Same; charges the surged price that was locked at the estimate |
| Surge | Write | — (multiplier 1.0) | Kafka (requests + locations) → demand / supply counter per H3 cell → Redis |
| Ride status | Read | Ride Service → SQL replica | Same, plus queue position while waiting |

**What the read path never touches:** Kafka, the matching worker, the trip tracker or the Payment Service. **What never touches SQL:** location pings.

## Deep dives to drive (learner leads, interviewer pushes)
1. **Geo indexing:** Redis `GEO` (geohash under the hood) vs. **H3 hexagon cells** vs. a quadtree. Why "my cell + neighbour cells" beats a radius query at scale; why hexagons (all neighbours are the same distance).
2. **No double booking:** two matchers pick the same driver; the same driver taps accept twice. Which line stops each? (Redis `NX`, then the conditional `UPDATE … WHERE status = …`.)
3. **No double charge:** `ride-ended` is delivered twice, or the processor call times out. Unique `rideId` row + idempotency key at the processor; on a timeout, **ask the processor** before retrying (the charge may have gone through).
4. **Card declined at the end:** the ride already happened. Options: authorize (hold) the price at request time, capture at the end; or charge at the end and chase failures. Trade-off: holds tie up the rider's money.
5. **Greedy vs. batch matching:** greedy gives each rider the nearest driver *now*; a 2 s batch finds a better total. Why only in dense cells?
6. **Location write load:** 250k writes/s. Shard Redis by city; drop out-of-order pings (`ts` older than stored); ping every 8 s while idle.
7. **Driver disconnects:** mid-offer, the lock TTL frees them and the location TTL removes them from search. Mid-ride, the ride continues; the rider's map freezes until pings resume.

## Interview follow-ups (answer these after the design)
- Two matching workers pick the same driver for two riders at the same moment. Walk through exactly what happens.
- The Payment Service calls the processor, and the call times out. Do you retry? What could go wrong either way?
- Why not store every location ping in SQL? What do you lose by keeping only the latest?
