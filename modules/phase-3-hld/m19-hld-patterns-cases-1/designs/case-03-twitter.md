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
Assumptions: 500M MAU, **200M DAU**, peak = 3× average, 1 day ≈ 10⁵ s.

| Item | Math | Result |
|---|---|---|
| Timeline loads | 200M × 10/day = 2B/day | **20k/s avg, 60k/s peak** |
| Tweets posted | 200M × 0.5/day = 100M/day | **1k/s avg, 3k/s peak** |
| Tweet rows hydrated | 60k/s × 20 tweets | ~1.2M reads/s at peak → **must come from cache** |
| Fan-out writes (avg 200 followers) | 100M × 200 = 20B/day | **200k/s avg, 600k/s peak** into the timeline cache |
| Celebrity tweet (50M followers) | 1 tweet × 50M | 50M inserts for one post → **why fan-out alone breaks** |
| Tweet storage | 100M × ~500 B = 50 GB/day | ~18 TB/year, ~90 TB over 5 years, ×3 replicas ≈ 270 TB |
| Media | 10% of tweets × ~500 KB = 5 TB/day | ~1.8 PB/year in Blob storage, served by the CDN |
| Timeline cache | 200M active users × 800 ids × 8 B | ~1.3 TB raw, ~3 TB with overhead → ~50 Redis nodes of 64 GB (plus replicas) |
| Hot-tweet cache | last ~2 days ≈ 200M tweets × 500 B | ~100 GB |
| Social graph | 500M users × 200 follows × 2 directions × ~16 B | ~3 TB, sharded by user_id |
| API egress (text) | 60k/s × 20 × 500 B | ~600 MB/s at peak; media goes through the CDN, not the API |
| Timeline API instances | 60k/s ÷ ~2.5k/s per instance | ~25 instances at peak; tweet writes need only 2–3 |

**What the numbers tell you:**
- Reads dominate, so precompute timelines.
- At 600k fan-out writes per second, the timeline must live in Redis, not SQL.
- Celebrities make pure push fan-out impossible, so use a **hybrid**.

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
