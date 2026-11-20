# Case 1: URL shortener

Diagram: [case-01-url-shortener.excalidraw](case-01-url-shortener.excalidraw), in two rows: **A. Regular link** and **B. Viral link**. In each row: User → Front Door, which then splits into a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension. The learner's own first attempt is kept in [case-01-url-shortener.attempt.excalidraw](case-01-url-shortener.attempt.excalidraw).

## Prompt (what the interviewer says)
> "Design a URL shortening service like bit.ly. Users paste a long URL and get a short link; anyone who opens the short link is redirected to the original."

## Functional requirements (in scope)
1. **Create a short link** from a long URL, on the website or through the API. Each request gets a **new** 7-character code, even for the same long URL, so each link has its own owner, expiry and stats.
2. **Redirect:** opening `sho.rt/{code}` sends the browser to the long URL.
3. **Custom alias** (optional): if the alias is taken, return **409** so the user can choose another.
4. **Expiry** (optional): links without a date never expire. An expired link returns **410 Gone**.
5. **Click counts** per link, readable by the owner (nice-to-have).

**Out of scope (say this out loud):** login details, editing a link's target, detailed analytics (geo dashboards, referrers UI), spam/malware scanning, billing.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Redirect latency | p99 < 100 ms, globally | Every click waits on it |
| Create latency | < 1 s | Rare and not time-critical |
| Availability | Redirects always up (AP) | A broken link breaks every page or print it appears on |
| Uniqueness | A code never maps to two URLs | Strong at create time only, enforced by one write region |
| Freshness | A new link may take ~1 s to work in other regions | Eventual consistency for reads is fine |
| Read:write ratio | 100:1 | The design is read-optimised |

## Capacity estimate
### 0. Facts (agree these with the interviewer first)
| Fact | Value |
|---|---|
| Creates | 100M per month |
| Redirects | 10B per month (100:1) |
| Code | 7 characters of base62 → 62⁷ ≈ 3.5 trillion codes |
| Link row | ~500 B (code, long URL, owner, created, expiry) |
| Peak / retention | Peak = 3× average; keep 5 years |
| Regions | Write: East US. Read: West Europe, Central India, Japan East |

Conversion: 1 month ≈ 30 × 10⁵ s ≈ 3 × 10⁶ s, so 100M/month ≈ 40/s.

### 1. API estimate → number of servers
One instance handles **~2,000 req/s** at 70% CPU. Keep at least 3 instances per region (one per availability zone).

| Service | Per month | Average | Peak | Instances at peak |
|---|---|---|---|---|
| Redirect (`GET /{code}`) | 10B | 4k/s | **12k/s** (worst case, edge cache cold) | 12k ÷ 2k = 6 → **12** (3 per region × 4) |
| Create (`POST /urls`) | 100M | 40/s | 120/s | 1 → **3** (write region) |
| Stats (`GET /urls/{code}/stats`) | small | — | — | **3** |
| Clicks consumer (Kafka consumer group) | 10B events | 4k/s | 12k/s | **2–3** |
| **Total** | | | | **~20 instances** |

Code space: 6B links in 5 years out of 3.5T codes ≈ 0.2% used, so a random code collides about once in 500 creates and one retry fixes it.

### 2. Redis estimate → number of nodes
Rule of thumb: use ~50 GB of each 64 GB node and plan on ~100k ops/s per node; add 1 replica per primary.

| Cluster | What it holds | Memory | Ops at peak | Primaries | Nodes with replicas |
|---|---|---|---|---|---|
| Redirect cache (per region) | Hot codes → long URL (~50M links × ~200 B × 2 overhead) | **~20 GB** | ≤ 12k reads/s | **1** (neither memory nor ops is close to the limit) | **2 per region → 8** |

Cache only successful lookups. A code never changes, so a long TTL is safe; a 404 is cached for a few seconds at most.

### 3. Database estimate (Cosmos DB)
**Why Cosmos:** every read is a lookup by one key (the code), and Cosmos copies data to other regions for you, so each region reads locally.
| Store | Key | Data (5 years) | Load at peak | Notes |
|---|---|---|---|---|
| `links` container | `id` = code, partition key `/id` | 6B × 500 B = **3 TB** (+ index ≈ 3.6 TB per region) | Reads on cache miss: ~2.4k RU/s; writes 120/s × ~10 RU ≈ 1.2k RU/s | One write region + 3 read regions. Cosmos splits it into ~72 physical partitions by itself; there's no manual sharding |
| Click counts table (a second Cosmos container) | `code` + minute → count | ~1 row per active code per minute; roll up to hourly after 7 days | ~2k writes/s at peak | Written only by the clicks consumer; read by the Stats Service |
| Kafka topic `clicks` (Azure Event Hubs, Kafka-compatible) | 32 partitions | 7-day retention | 12k events/s → **12 throughput units** (1,000 events/s each) | Absorbs click spikes |

### What the numbers tell you
- Writes are tiny (120/s), so **one write region** is plenty. That also makes code uniqueness easy.
- Reads are 100× writes and latency-critical everywhere, so use **Front Door + Redis + read regions**.
- 3 TB is small: the store is chosen for **global reads and point lookups**, not for size.
- The hard parts are **global latency and uniqueness**, not volume.

## High-level design (see the diagram)
**Create (write path):**
1. Client → Front Door → APIM (auth, rate limit) → Create Service.
2. Generate a random 7-character base62 code and insert `{id: code, longUrl, ownerId, expiresAt}` into the Cosmos **write region**.
3. If the insert returns **409**, the code is taken: generate a new one and retry (max 5). A custom alias that's taken returns 409 to the user.
4. Return `201 {code, shortUrl}`. Cosmos replicates to the read regions **asynchronously** (~1 s).

**Redirect (read path):**
1. Browser → Front Door. If the 302 is in the edge cache (short TTL, never past expiry), it's returned at the edge.
2. Otherwise → Redirect Service in the nearest region → Redis → nearest Cosmos read region on a miss.
3. If the code isn't found there, read the **write region** once (a brand-new code may not have replicated yet) before returning 404.
4. Expired → **410**. Otherwise → **302** with `Location: longUrl`.
5. Record the click by adding an event to an in-memory buffer that the Kafka producer sends to the `clicks` topic in batches. The redirect doesn't wait for it.

**Clicks:** Kafka topic `clicks` → clicks consumer (a consumer group that counts per code per minute) → **click counts table**, a second Cosmos container with one row per code per minute (`code, minute, count`). The Stats Service reads it for the owner. For edge-cached clicks, count from **Front Door access logs** instead, because those clicks never reach the Redirect Service.

**Kafka vs. Event Hubs:** Event Hubs is Azure's managed event stream and accepts the Kafka protocol, so the design uses Kafka terms: topic, partition, producer, consumer group.

## Which components each path touches
| Requirement | Path | Regular link (row A) | Viral link (row B) |
|---|---|---|---|
| Create | Write | Front Door → APIM → Create Service → Cosmos write region → (async) read regions | Same |
| Redirect | Read | Front Door → Redirect Service → Redis → Cosmos read region (miss) → write region (not found) | Front Door **edge cache** answers ~99% of clicks; the rest go to the in-process cache (5 s) → Redis (hot key with replicas) → Cosmos (almost never) |
| Click counts | Write | Redirect Service → Kafka topic `clicks` → clicks consumer → click counts table | Front Door logs → Kafka topic (partition by code) → clicks consumer pre-aggregates in memory → 1 write per minute |
| Delete / abuse | Write | Create Service → Cosmos (deleted → 410) | Same + **purge** the Front Door cache and `DEL` the Redis key |
| Stats | Read | APIM → Stats Service → click counts table | Same |

**What the redirect path never touches:** APIM, the Create Service, or the write region (except for a code that hasn't replicated yet).

## Deep dives to drive (learner leads, interviewer pushes)
1. **Code generation:** random + retry on 409, vs. a counter (ranges of 1,000 per instance from Redis `INCRBY`, shuffled before encoding), vs. a hash of the URL. Why random fits here: unguessable and no coordination.
2. **301 vs. 302:** 301 is cached by browsers forever, so you lose clicks and can't take a link down. 302 plus an edge cache with a short TTL keeps you in control.
3. **Multi-region writes:** two regions could insert the same code at once, and last-write-wins would silently overwrite one. That's why there's a single write region.
4. **Read-after-create across regions:** fall back to the write region on not-found, and never cache 404s.
5. **Hot link:** edge cache + in-process cache + Redis replicas; count clicks from edge logs.
6. **Expiry:** check `expiresAt` on every read (410); Cosmos TTL deletes the expired documents later; the edge TTL must never be longer than the remaining lifetime.

## Interview follow-ups (answer these after the design)
- How many characters do you need, and how did you work it out?
- What happens if two users ask for the same custom alias at the same moment?
- A link is reported as malware. How quickly can you stop it redirecting everywhere?

## Learner's first attempt: review (6.5/10, 2026-11-18)
The decisions were good: random code + 409 retry, a new code per request, 302 + edge TTL, one write region, and the `id = code` data model. Points lost for:
- **Estimates:** the per-month numbers were treated as per day (1,000 writes/s instead of 40/s), and storage was computed from reads instead of writes.
- **Diagram:** the redirect service didn't look anything up (no Redis or Cosmos arrow); the services were duplicated (2 get + 2 post + 1 redirect) instead of create + redirect, with regions drawn as boxes.
- **Text vs. decisions:** "same code for the same URL" and "strong read replicas" contradicted what was agreed; the API used action names and a JSON response instead of `GET /{code}` → 302.
