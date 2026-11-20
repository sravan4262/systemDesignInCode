# Case 5: Uber (ride hailing)

Diagram: [case-05-uber.excalidraw](case-05-uber.excalidraw), in two rows: **A. Normal ride** and **B. Hot spot** (a stadium lets out: 50k requests in 15 minutes, 2k drivers nearby). Two clients (driver app, rider app) → one tall gateway (APIM for REST + a WebSocket gateway) → a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design Uber. Riders request a ride, and a nearby driver picks them up."

## Functional requirements (in scope)
1. **Drivers share their location** every ~4 s while online.
2. **Fare estimate:** the rider enters pickup and dropoff and sees a price (including surge).
3. **Request a ride:** the system finds a nearby available driver and **offers** the ride; the driver has 10 s to accept, else the next driver is tried.
4. **Live tracking:** once matched, the rider sees the driver approach and the trip on the map.
5. **Trip lifecycle:** accept → start → end; the fare is computed at the end and handed to payments; riders and drivers see their trip history.

**Out of scope (say this out loud):** payment processing itself (Case 7: we emit `ride-completed`), ratings, pooled / shared rides, scheduled rides, maps and routing (an external Maps API gives routes and ETAs), driver onboarding, fraud.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Matching time | A driver offer within ~5 s of the request | Riders open a competitor app after ~1 minute |
| Location freshness | The rider's map lags the driver by ≤ ~5 s | Older is useless for pickup |
| Consistency of assignment | **One driver per ride, one ride per driver**, always (CP for this step) | Double-booking a driver is the worst bug in the system |
| Availability | Location ingest and tracking keep working (AP) | A dropped ping is fine; the next one comes in 4 s |
| Durability | Rides, fares and status changes are never lost | They're money (payments, payouts) |
| Geography | Global, but every ride is **local to one city** | Shard everything by city / region |

## Capacity estimate (high level)
**Facts:** 25M rides/day; ~1M drivers online at peak, each pinging every 4 s; an average trip is 20 min; peak = 3× average; 1 day ≈ 10⁵ s. Hot spot: 50k requests in 15 min in a few cells.

| What | Math | Result | So… |
|---|---|---|---|
| Location pings/s | 1M ÷ 4 s | **~250k/s** | The heaviest write by far; keep **latest only** in Redis, never a DB row per ping |
| Latest-location memory | 1M × ~100 B | **~100 MB** | Fits in RAM easily; sharding is for **write throughput** (by city), not size |
| Ride requests/s | 25M ÷ 10⁵ | **250/s avg, ~1k/s peak** | Light; SQL is fine |
| Concurrent trips | 25M × 20 min ÷ 1,440 min | **~350k avg, ~1M peak** | ~250k tracking pushes/s to riders (one per driver ping) |
| Location history | 250k/s × 100 B | **~2 TB/day** | Kafka → cheap storage (data lake) for receipts and disputes, not the hot path |
| Hot spot | 50k requests ÷ 900 s | **~55 requests/s in a few cells** | The rate is small; the problem is **contention** (many requests, few drivers) and **pricing** |

**What the numbers tell you:** this is a **write-heavy, real-time** system: 250k location writes/s that are only useful for seconds, so they live in memory and are overwritten. Ride requests are rare by comparison; their difficulty is **correctness** (no double booking), not volume. Everything is local to a city, so the city is the natural shard key.

## API
**WebSocket** `wss://rt.example.com/ws` (JWT on connect; drivers while online, riders while a ride is active)

| Direction | Frame | Payload | Notes |
|---|---|---|---|
| Driver → server | `location` | `{ lat, lng, heading, ts }` every 4 s | Fire and forget; no ack per ping |
| Server → driver | `offer` | `{ rideId, pickup, dropoff, fare, expiresInSec: 10 }` | Driver answers with `POST /rides/{id}/accept` |
| Server → rider | `ride` | `{ rideId, status, driver?, etaSec }` | Matched, arriving, started, ended |
| Server → rider | `driverLocation` | `{ lat, lng, heading }` every 4 s | Only during a ride |

**REST**

| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /fare-estimates` | `{ pickup, dropoff }` | `200 { estimateId, price, surge, expiresAt }` | Price is locked for ~2 min |
| `POST /rides` | `{ estimateId }` + `Idempotency-Key` | `202 { rideId, status: matching }` | A double tap creates one ride |
| `GET /rides/{id}` | — | `200 { status, driver?, etaSec, fare? }` | Fallback if the WebSocket drops |
| `POST /rides/{id}/accept` (driver) | — | `200` · `409` (taken, expired or you're busy) | The **one** contested write |
| `POST /rides/{id}/start` · `/end` (driver) | `{ odometer?, ts }` | `200 { fare? }` | End → `ride-completed` → payments |
| `POST /rides/{id}/cancel` (rider) | — | `200 { cancelFee? }` | Frees the driver |
| `GET /drivers/nearby?lat=&lng=` | — | `200 { drivers: [{ lat, lng, heading }] }` | Approximate and anonymous (the cars on the map) |
| `GET /rides?role=rider&cursor=` | — | `200 { rides[], nextCursor }` | History |

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| WebSocket gateway | Holds driver connections (and riders during a ride); registers `user → gateway node` in presence | Like Case 2; ~200k connections per node |
| Location Service | Takes pings; overwrites the driver's latest position; publishes the ping | **Redis geo index** (`GEOADD drivers:{city}`; status `available / busy` alongside), **Kafka** `driver-locations` |
| Trip tracker | For pings from drivers on a trip, pushes the position to the rider's gateway | Kafka consumer; driver → rideId map cached in Redis |
| Ride Service | Creates rides, accepts, starts, ends, cancels | **Azure SQL, sharded by city**: `rides`, `drivers` (status), outbox |
| Matching worker | Finds nearby available drivers, ranks by ETA, locks one, sends the offer, waits 10 s, moves on | Kafka consumer of `ride-requested` (partitioned by city); **Redis** driver locks |
| Pricing Service | Fare estimate = route (Maps API) × rates × surge | Maps API; surge multipliers in **Redis** |
| Surge (row B) | Counts requests vs. available drivers per H3 cell every 30 s → multiplier | Stream consumer on Kafka → Redis |
| Map Service | Nearby cars for the rider's map | Redis `GEOSEARCH` (row B: per-cell snapshot cache) |

**Write path:**
1. **Locations:** driver → WebSocket gateway → Location Service → `GEOADD` (latest only, TTL 30 s so a vanished driver drops out) and → Kafka `driver-locations`. The trip tracker consumes it and, only if the driver is on a ride, pushes the position to the rider's gateway (presence lookup, as in Case 2).
2. **Request:** rider → `POST /rides` → Ride Service inserts `rides(status = matching)` + outbox → Kafka `ride-requested` → `202` to the rider.
3. **Match:** the matching worker runs `GEOSEARCH` (2 km, 10 nearest available), ranks them by ETA (Maps API), takes the best and **locks** it: `SET driver:{id}:offer rideId NX EX 15`. If the lock fails, another ride is offering to that driver, so try the next one. Then it pushes the offer.
4. **Accept (the contested write):** one SQL transaction on the city shard:
   `UPDATE rides SET driver_id = @d, status = 'accepted' WHERE id = @r AND status = 'matching'` and
   `UPDATE drivers SET status = 'busy' WHERE id = @d AND status = 'available'`.
   Both must change exactly 1 row, else roll back and return `409`. **The Redis lock is a fast filter; this conditional update is the truth.**
5. No answer in 10 s → the lock expires, the worker offers the next driver. No driver after N tries / 60 s → `no_drivers`.
6. **Start / end** update the ride; `end` computes the fare and emits `ride-completed` (outbox → Kafka) for payments (Case 7) and receipts.

**Read path:**
1. **Fare estimate:** Pricing Service → Maps API (route, ETA) × rates × surge multiplier from Redis.
2. **Nearby cars:** Map Service → `GEOSEARCH` on the geo index.
3. **Ride status / history:** Ride Service → SQL read replica. Live updates come by **push**; `GET /rides/{id}` is the fallback after a reconnect.

## Which components each path touches
| Requirement | Path | Normal ride (row A) | Hot spot (row B) |
|---|---|---|---|
| Driver location | Write | Gateway → Location Service → geo index (latest) + Kafka → trip tracker → rider push (only on a trip) | Same |
| Request + match | Write | Ride Service → Rides (SQL) + outbox → Kafka → matching worker (greedy: nearest first) → driver lock → offer | Same up to Kafka → **batch matcher per cell every 2 s** (N riders × M drivers, minimise total ETA) → locks → offers; the rest wait for the next round |
| Accept / start / end | Write | Ride Service → one conditional UPDATE on rides + drivers → ride-events → rider push, payments | Same (the conditional update makes simultaneous accepts safe) |
| Surge | Write | — (multiplier 1.0) | Kafka (requests + locations) → demand / supply counter per H3 cell → surge multipliers (Redis) |
| Fare estimate | Read | Pricing → Maps API | Pricing → **surge multiplier** (Redis) × route |
| Nearby cars | Read | Map Service → `GEOSEARCH` | Map Service → **per-cell snapshot cache (2 s)**, shared by everyone in the stadium → geo index on a miss |
| Ride status | Read | Ride Service → SQL replica | Same, plus queue position while waiting |

**What the read path never touches:** Kafka, the matching worker or the trip tracker. **What never touches SQL:** location pings.

## Deep dives to drive (learner leads, interviewer pushes)
1. **Geo indexing:** Redis `GEO` (geohash under the hood) vs. **H3 hexagon cells** vs. a quadtree. Why cells with a ring search ("my cell + neighbours") beat a radius query at scale; why hexagons (all neighbours are the same distance).
2. **No double booking:** walk through two riders' matchers picking the same driver, and the same driver tapping accept twice. Which line of code stops each one? (Redis `NX` lock, then the conditional `UPDATE … WHERE status = …`.)
3. **Greedy vs. batch matching:** greedy gives each rider the nearest driver *now*; batching over 2 s finds a better total. Why batch only in dense cells?
4. **Location write load:** 250k writes/s. Shard Redis by city; drop pings that arrive out of order (`ts` older than stored); reduce to every 8 s while the driver is idle.
5. **Driver disconnects mid-offer:** the lock TTL frees them; the TTL on their location removes them from search. Mid-trip disconnect: the trip continues; the app resends buffered points when it reconnects.
6. **Surge:** computed per cell per 30 s window from demand / supply; the **estimate locks the price** for 2 minutes so the rider pays what they saw.
7. **Regional failure:** each city lives in one region. Active-passive per region (M21); in-flight rides are in SQL, so they survive a failover; locations rebuild themselves within 4 s from fresh pings.

## Interview follow-ups (answer these after the design)
- Two matching workers pick the same driver for two riders at the same moment. Walk through exactly what happens.
- A driver accepts, but their phone loses the connection before the `200` arrives, and they tap accept again. What does the server return, and why is it safe?
- Why not store every location ping in SQL? What would you lose by keeping only the latest?
