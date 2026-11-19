# Case 3: Twitter / news feed

Diagram: [case-03-twitter.excalidraw](case-03-twitter.excalidraw). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design Twitter. Users post short tweets, follow other users, and see a home timeline of tweets from people they follow."

## Functional requirements (in scope)
1. **Post a tweet:** up to 280 characters, with optional media (images or short video).
2. **Follow and unfollow** another user. Following is one-way, with no approval step.
3. **Home timeline:** tweets from the people I follow, newest first, paginated 20 at a time.
4. **User timeline:** one user's own tweets, newest first.
5. **Like and retweet** a tweet, with counts shown on each tweet.

**Out of scope (say this out loud):** DMs, ads, trends and hashtag ranking, notifications, ranked or ML feeds (we use reverse-chronological order), and edit/delete beyond a soft delete. **Search** is a stretch goal: there is a hook in the diagram, but no deep dive.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Home timeline latency | p99 < 200 ms | It is the product's main screen |
| Availability | 99.99% for reads | Availability beats consistency (AP): a stale feed is fine, an empty one is not |
| Freshness | A new tweet reaches followers' timelines within ~5 s | Eventual consistency is fine |
| Read-your-writes | My own tweet appears in my own timeline immediately | Otherwise users think the post failed |
| Durability | A posted tweet is never lost | It is acknowledged only after the DB write |
| Read:write ratio | ~20:1 (requests), ~400:1 (tweet rows read) | The design is read-optimised |

## Capacity estimate
### 0. Facts (agree these with the interviewer first)
| Fact | Value |
|---|---|
| Users | 500M MAU, **200M DAU** |
| Timeline loads | 10 per user per day, 20 tweets per page |
| Tweets | 0.5 per user per day → 100M/day; 10% have media (~500 KB) |
| Likes / follows | 5 likes and 0.5 follows per user per day |
| Followers | 200 on average; celebrities up to 100M |
| Sizes | Tweet row ~500 B; tweet id 8 B (Snowflake) |
| Peak / retention | Peak = 3× average; keep 5 years |

Conversion: 1 day ≈ 10⁵ s, so 100M/day ≈ 1,000/s.

### 1. API estimate → number of servers
One instance handles **~2,000 req/s** at 70% CPU. Keep at least 3 instances (one per availability zone) for availability, and autoscale between the minimum and the peak count.

| Service | Per day | Average | Peak | Instances at peak |
|---|---|---|---|---|
| Timeline reads (`GET /timeline`) | 200M × 10 = 2B | 20k/s | **60k/s** | 60k ÷ 2k = **30** (min 10) |
| Post tweet (`POST /tweets`) | 100M | 1k/s | 3k/s | 2 → **3** (availability) |
| Like / retweet | 200M × 5 = 1B | 10k/s | 30k/s | **15** |
| Follow / unfollow | 100M | 1k/s | 3k/s | 2 → **3** |
| Fan-out workers (Kafka consumers) | 20B Redis writes | 200k/s | **600k/s** | ~20k pipelined writes/s each → **30 workers** |
| **Total** | | | | **~80 instances at peak** |

Egress (text only): 60k/s × 20 tweets × 500 B ≈ **600 MB/s** at peak. Media is not served by the API; it goes through the CDN.

### 2. Redis estimate → number of nodes
Rule of thumb: a node has 64 GB of RAM, but **use only ~50 GB** (leave headroom for fragmentation and for snapshot forks), and handles **~100k ops/s**. Size each cluster by memory **and** by throughput, and take the larger. Add **1 replica per primary**.

**a) Timeline cache:** one Redis list of tweet ids per active user.
```
memory  = 200M users × 800 ids × 8 B        = 1.3 TB raw
        × ~2 (list + key overhead)           ≈ 2.6 TB
nodes   = 2.6 TB ÷ 50 GB                     ≈ 52 primaries   ← memory decides
ops     = 600k writes/s + 60k reads/s peak   ≈ 7 primaries' worth (not the limit)
total   = 52 primaries + 52 replicas         ≈ 104 nodes
```
Trade-off: keep 200 ids instead of 800 → **~13 primaries** (26 nodes). Older pages fall back to a pull from the Tweets store.

**b) Tweet cache** (hydrating ids into tweets, plus like counters):
```
memory  = 2 days × 100M tweets × 500 B × 1.5 overhead ≈ 150 GB → 3 primaries
ops     = 60k timeline reads/s × 20 tweets             = 1.2M key reads/s
        ÷ ~150k/s per node (MGET batching)             ≈ 8 primaries   ← throughput decides
total   = 8 primaries + 8 replicas                     = 16 nodes
```
Celebrity tweets are hot keys, so add a short-TTL **in-process cache** on the Timeline Service in front of Redis.

**Redis total: ~120 nodes** (or ~42 with 200-id timelines).

### 3. Database estimate → number of shards and replicas
Rule of thumb: keep **~2 TB of data per node or shard** (disks of about 4 TB leave room for compaction and rebuilds). **Replication factor 3**, spread over 3 availability zones.

**a) Tweets store: Cassandra.** It is write-heavy, append-only and needs no joins.
- Table `tweets`: partition key `tweet_id`, used to hydrate tweets.
- Table `user_tweets`: key `(user_id, month)`, clustering `tweet_id DESC`, ids only. It serves user timelines and the celebrity pull.
```
data    = 100M × 500 B = 50 GB/day → 18 TB/year → 90 TB over 5 years
× RF 3  = 270 TB on disk
nodes   = 270 TB ÷ 2 TB per node            ≈ 135 nodes (5 years)
year 1  = 18 TB × 3 ÷ 2 TB                  ≈ 27 nodes, then add nodes as it grows
reads   = cache misses ~5% × 1.2M/s         = 60k/s ÷ 135 nodes ≈ 450/s each (easy)
writes  = 3k/s peak                         (trivial)          ← storage decides
```
The same data on sharded SQL: 90 TB ÷ 2 TB = **45 shards**, each with 1 primary + 2 replicas = **135 servers**. Same size, but you run the sharding yourself (M6). Moving tweets older than a year to cheaper storage reduces this a lot.

**b) Social graph: SQL** (or Cassandra), sharded by `user_id`. There are two tables, `followers(user_id, follower_id)` and `following(user_id, followee_id)`, so each lookup hits a single shard.
```
data    = 500M users × 200 edges × 2 directions × 16 B ≈ 3.2 TB
shards  = 3.2 TB ÷ ~1 TB per shard (headroom for growth) ≈ 4 → choose 8 (using fixed buckets, M6)
servers = 8 shards × (1 primary + 2 replicas)            = 24
reads   = fan-out: 3k tweets/s × paged follower lists    (fine)
```
A celebrity's follower list (100M × 16 B ≈ 1.6 GB) sits on one shard. That is acceptable because the hybrid model never fans it out.

**c) Likes:** 1B/day × ~24 B ≈ 24 GB/day ≈ 9 TB/year. Store them in Cassandra keyed by `tweet_id`. They can share the tweets cluster or get their own; counts are served from Redis counters.

**d) Kafka:** 3k events/s × 1 KB = 3 MB/s, which is tiny. Use **3 brokers, RF 3, 64 partitions**. The partition count is set by the parallelism of the fan-out workers (at least 30), not by volume.

**e) Media:** 5 TB/day → ~1.8 PB/year in Blob storage (tiered hot → cool → archive), served by the CDN.

### What the numbers tell you
- Reads dominate, so build timelines in advance (fan-out on write).
- At 600k fan-out writes/s, timelines must live in Redis, not in SQL. The memory needed (2.6 TB) is what sets the node count.
- A celebrity tweet means 50M inserts, so pure push breaks. Use a **hybrid**: push for normal users, pull for celebrities.
- The Tweets store is sized by **storage**, not by load (cache hit rate around 95%).

## High-level design (see the diagram)
**Write path:**
1. Client → Gateway → Tweet Service.
2. The service gets a Snowflake ID and writes the tweet to the Tweets store. The tweet goes into the outbox in the same transaction.
3. The outbox publishes `tweet-created` to Kafka.
4. Fan-out workers fetch the follower list from the graph store and push the tweet id onto each follower's timeline list in Redis (`LPUSH` + `LTRIM` to 800).
5. **Celebrities (over ~1M followers) are skipped here.**

**Read path:**
1. Client → Gateway → Timeline Service.
2. The service reads the tweet ids from my Redis timeline list.
3. It merges in recent tweets from the celebrities I follow (a pull at read time) and my own latest tweets (read-your-writes).
4. It hydrates the ids from the tweet cache, falling back to the Tweets store on a miss.
5. It returns 20 tweets and a cursor.

**Media:** the client uploads straight to Blob storage with a presigned (SAS) URL, and the tweet stores only the media URL. Reads come from the CDN.

## Deep dives to drive (learner leads, interviewer pushes)
1. **Fan-out on write vs. on read vs. hybrid:** what decides the celebrity threshold?
2. **Inactive users:** don't fan out to users who haven't logged in for 30+ days; rebuild their timeline on login. What does that rebuild cost?
3. **Unfollow:** what happens to the tweets already in my timeline? Filter them at read time, or rebuild?
4. **IDs and ordering:** Snowflake (time + node + sequence) gives sortable ids, which makes cursor pagination `max_id`-based (keyset, as in M6).
5. **Like counts:** at 100k likes/s on a viral tweet, a single row becomes a hot key. Use sharded counters plus async aggregation; an approximate count is fine.
6. **Failure modes:**
   - A Redis node is lost: rebuild from the Tweets store and graph.
   - Kafka lag: freshness drops, but correctness holds.
   - A duplicate event: the fan-out must be idempotent (check whether the id is already present, or accept the dup and dedupe at read).
7. **Hot partitions:** a celebrity's tweet row is read by millions. Spread it across cache replicas and add a local in-process cache.

## Interview follow-ups (answer these after the design)
- Why not just `SELECT ... WHERE author IN (my 2,000 followees) ORDER BY time LIMIT 20`?
- A user follows 5 celebrities and 300 normal users. Walk through exactly how their timeline is built.
- Redis loses a shard of timelines. What does the user see, and how do you recover?
