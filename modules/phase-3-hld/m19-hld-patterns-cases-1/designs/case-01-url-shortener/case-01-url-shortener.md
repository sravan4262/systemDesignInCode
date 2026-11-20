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

## Capacity estimate (high level)
**Facts:** 100M creates/month, 10B redirects/month (100:1); link row ~500 B; keep 5 years; peak = 3× average; 1 month ≈ 3 × 10⁶ s.

| What | Math | Result | So… |
|---|---|---|---|
| Creates/s | 100M ÷ 3 × 10⁶ | **40/s avg, 120/s peak** | Tiny: one Cosmos write region is plenty, which also makes codes unique |
| Redirects/s | 10B ÷ 3 × 10⁶ | **4k/s avg, 12k/s peak** | Front Door cache + Redis + read copy in each region; ~3 redirect instances per region |
| Code length | 62⁷ vs. 6B links | **3.5T codes, 0.2% used** | 7 base62 characters; a random code rarely collides, so retry on 409 |
| Storage | 6B × 500 B | **~3 TB in 5 years** | Small: Cosmos splits it by itself, no manual sharding |
| Click events | = redirects | **12k/s peak** | Kafka topic + a consumer that writes one row per code per minute |

**Why Cosmos:** every read is a lookup by one key (the code), and Cosmos copies data to other regions for you, so each region reads locally.

**What the numbers tell you:** writes are tiny and reads are 100× more and latency-critical worldwide, so the design is all about **reading close to the user** (edge, Redis, regional read copies). Volume is not the hard part; **global latency and unique codes** are.

## API
| Endpoint | Request | Response | Errors |
|---|---|---|---|
| `POST /urls` (APIM, auth) | `{ longUrl, customAlias?, expiresAt? }` | `201 { code, shortUrl, expiresAt }` | `400` invalid URL · `409` alias taken |
| `GET /{code}` (public, via Front Door) | — | `302` + `Location: <longUrl>` (no body) | `404` unknown · `410` expired or deleted |
| `GET /urls/{code}/stats` (APIM, owner only) | — | `200 { code, clicks, clicksByDay[] }` | `403` not owner · `404` |
| `DELETE /urls/{code}` (APIM, owner or abuse team) | — | `204` (then purge Front Door + Redis) | `403` · `404` |

- The redirect is a browser request, so it returns a **302 with a Location header**, not JSON; the browser follows it by itself.
- Clicks are recorded **inside** `GET /{code}` (an async event), so there's no separate "track click" call a client could skip or fake.

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
