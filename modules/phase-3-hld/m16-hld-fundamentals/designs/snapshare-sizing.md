# SnapShare sizing (back-of-envelope worksheet, M16 Session 4)

Worked level by level with the learner, corrections included.

## Facts
| Fact | Value |
|---|---|
| Daily active users | 10M |
| Feed loads | 10 per user per day, 1 API call each, 20 posts per load |
| Posting | 10% of users post 1 photo/day |
| Likes | 5 per user per day |
| Photo | ~500 KB (feed thumbnail ~50 KB) |
| Post metadata | ~1 KB |
| Retention | 5 years |
| Peak | 3× average |

Conversion: 1 day ≈ 10⁵ s, so 100M/day ≈ 1,000/s.

## Level 1: API requests
| Service | Per day | Average | Peak | Servers (≈2,800 req/s each at 70%) |
|---|---|---|---|---|
| Feed reads | 100M | 1,000/s | 3,000/s | 2–3 (min for availability) |
| New posts (metadata only; photo goes direct to Blob via SAS) | 1M | 10/s | 30/s | 2 (availability) |
| Likes | 50M | 500/s | 1,500/s | 2 (availability) |
Autoscale between a minimum (2–3) and a maximum. (Learner's first pass used 10,000/s for 100M/day: 10× slip.)

## Level 2: images (the biggest load)
```
images/day   = 100M feed loads × 20      = 2 × 10⁹
images/sec   = 2 × 10⁹ ÷ 10⁵              = 20,000/s avg, 60,000/s peak
bandwidth    = 60,000 × 500 KB            = 30 GB/s peak  (≈ 240 Gbit/s)
per user     = 10 loads × 20 × 500 KB     = 100 MB/day
```
So: serve from the CDN edge (Front Door), resize on upload (worker on BlobCreated) to ~50 KB WebP/AVIF thumbnails → 3 GB/s peak and ~10 MB/day per user; lazy-load as the user scrolls.

## Level 3: Redis (feed cache)
```
memory     = 10M users × 20 KB = 200 GB  × 1.5 overhead ≈ 300 GB
throughput = 3,000 reads/s vs ~100,000 ops/s per node → 1 node would do
```
Memory, not throughput, decides: ~300 GB ÷ ~30 GB per shard ≈ 10 shards (+ a replica each).
Better: cache only recently active users' feeds (~20% → ~60 GB → ~2 shards), rebuild others on demand with a TTL.
Routing: keys hash into 16,384 slots; the client library (one ConnectionMultiplexer) sends each key to the right shard.

## Level 4: SQL
```
reads reaching SQL = 3,000 × 10% miss   =   300/s
writes             = 30 posts + 1,500 likes = 1,530/s
total                                   ≈ 1,830 ops/s
```
One 8-vCore Azure SQL (~10K simple reads/s, ~3–5K small writes/s, writes capped by transaction-log speed) handles it, plus a zone-redundant replica for failover. Redis cut the load from ~4,500/s.

## Level 5: storage (5 years)
| Data | Per day | 5 years | Where |
|---|---|---|---|
| Photos | 1M × 500 KB = 500 GB | ~1 PB (learner said 2 PB: slip) | Blob Storage, older photos → cool tier |
| Post metadata | 1 GB | ~2 TB | SQL |
| Likes | 50M × ~100 B = 5 GB | ~10 TB | SQL Hyperscale (fits, up to ~100 TB) or Cosmos / partitioned table |

## Hot spot: a viral post (5,000 likes/s on one row)
Problem: every `UPDATE Posts SET Likes = Likes + 1` needs the X lock on the same row → serialized → lock waits, timeouts, pool exhaustion.
Fix (learner's idea, refined): INSERT a Likes row (UNIQUE UserId+PostId, different rows → no hot lock) + Redis `INCR likes:post:42` for the visible count + a flush job every few seconds doing one `UPDATE … Likes = Likes + @delta`. Likes rows are the source of truth (recompute if Redis loses deltas).
