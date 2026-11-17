# M8 — Redis and distributed caching

> Plan: `Senior_Engineer_Interview_Plan.html#m8` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Measured latency/throughput differences and a written explanation of consistency and invalidation trade-offs.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
Your M4 LRU cache, moved **out of the process** so every server shares it. A cache makes reads fast and cheap, but every cache is a **second copy of the data**. The hard parts are keeping that copy correct (invalidation) and surviving when the cache fails or empties all at once.

### Sessions (in order)
1. **Why a distributed cache:** in-process vs. Redis, latency numbers, hit ratio as the key health metric.
2. **Caching patterns:** cache-aside, read-through, write-through, write-around, write-behind. 🧪 Cache-aside in ASP.NET Core with Redis.
3. **Expiry and eviction:** TTL, Redis's approximate LRU/LFU, memory limits.
4. **Redis data structures and atomic operations:** strings, hashes, sets, sorted sets (leaderboards), `INCR`, Lua scripts.
5. **Invalidation and consistency:** update the DB then delete the cache entry; the races that remain; stale reads.
6. **Failure modes:** stampede (dogpile), penetration, avalanche (mass expiry), hot keys. 🧪 Cause a stampede, then fix it (single-flight lock, jittered TTL).
7. **Distributed rate limiting:** your token bucket as a Redis Lua script. 🧪
8. **When Redis is down:** fallback, timeouts, circuit breaker, restart and failover. 🧪 Measure DB-only vs. cached throughput.

### Done when you can answer
- Cache-aside vs. write-through: what are the trade-offs?
- A hot key expires and the database falls over. What happened, and how do you prevent it?
- Why delete the cache entry after a write instead of updating it? What race is still left?
- What happens to your app when Redis goes down?

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### M8 overview (2026-11-17, all 8 sessions at overview level by choice; no labs)
- **Why distributed:** in-process (~100 ns, per server, inconsistent after updates, cold after deploy) vs. Redis (~1 ms, one shared copy). Two-level / `HybridCache` for the hottest keys. Hit ratio is the health metric (95% → 1 in 20 reads hits the DB; a drop multiplies DB load).
- **Patterns:** cache-aside (default; DB = source of truth, survives Redis down), read-through (`HybridCache.GetOrCreateAsync`), write-through (always warm, slower writes, caches unread data), write-around, write-behind (counters/bursts; loses unflushed writes; never money).
- **Expiry/eviction:** always a TTL, with jitter; TTL = staleness cap, invalidation does the real work. `allkeys-lru` for a pure cache; Redis LRU is approximate (sampling); `noeviction`/separate instance when Redis holds data you can't lose (rate limits, locks).
- **Data structures:** commands are atomic (single thread): `INCR` counters, sorted sets for leaderboards/top-N (`ZINCRBY`, `ZREVRANGE`), `SET NX EX` locks, Lua for multi-step atomic logic.
- **Invalidation:** update DB first, then DELETE the key (updating races: last writer may write the older value). Remaining race: slow reader re-populates the old value after the delete → TTL caps it, delayed double delete, versioning. Reliable invalidation via outbox/CDC; pub/sub to clear local caches. Caches are eventually consistent: balances/stock-at-checkout read from the DB.
- **Failure modes:** stampede (hot key expires → single-flight lock, early refresh, serve stale), avalanche (mass expiry/cold restart → jitter, warm-up, protect DB), penetration (missing ids → cache "not found", Bloom filter), hot key (one node → local cache in front, key copies, split counters).
- **Rate limiting:** in-process limiters multiply by server count; shared token bucket per client in Redis as a Lua script (atomic refill + take); one bucket per rule, check-all-then-take-all in one script, hash tags in Cluster. Gateway (APIM policies: per IP, subscription, JWT claim, quotas) for request-level limits; app + Redis for limits needing business data. 429 + Retry-After; fail open with a local fallback.
- **Redis down:** short timeouts (ms, not the 5 s default), one shared `ConnectionMultiplexer`, catch and fall back to the DB, circuit breaker (M13), protect the DB (local cache, single-flight, bulkhead), per-use decisions (cache → DB, rate limit → fail open, locks → fail closed). Replica + auto-failover (async: may lose last writes), Cluster (16,384 slots), plan for a cold cache.

#### M8 Q&A (2026-11-17): ~2.9 / 6
- Strong: rate limits across servers (shared state in Redis), product descriptions/countries caching.
- Revisit: name the failure (stampede; fix = single-flight + early refresh, not "re-cache"); the remaining delete race (slow reader) + TTL cap; Redis down must also protect the DB (local cache, single-flight, bulkhead) and the "stop calling, test periodically" is a circuit breaker; don't cache balances because they must be current (not because of traffic); arbitrary search queries → low hit ratio, don't cache. Skipped: cache-aside vs. write-through.

#### <Topic name>
- **In plain words:**
- **Problem it solves:**
- **Analogy:**
- **Diagram:** (ASCII/Mermaid or a link to `designs/`)
- **Variations / alternatives:**
- **When not to use it:**
- **Quick-check answers:**

### Problem
_What real engineering problem does this concept solve?_

### Internals
_How does it work? What guarantees does it provide?_

### Implementation
_What did I build? Code paths, schema, configuration, tests._

### Failure
_What did I deliberately break? What happened? (Tick the matching items in the plan's section 4.)_

### Recovery
_How does the system detect, recover, or reconcile the failure?_

### Trade-offs
_What alternatives exist? Why choose this design?_

### Production
_What should be monitored? What are the operational risks?_

### Interview
_Follow-up questions an interviewer could ask, with my short answers._

### My notes
_Which Apple Notes did this update or correct?_
