# Case 3: Twitter / news feed

Diagram: [case-03-twitter.excalidraw](case-03-twitter.excalidraw), in two rows: **A. Regular user** and **B. Celebrity**. In each row: User → API Gateway, which then splits into a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design Twitter. Users post short tweets, follow other users, and see a home timeline of tweets from people they follow."

## Functional requirements (in scope)
1. **Post a tweet:** up to 280 characters, with optional media (images or short video).
2. **Follow and unfollow** another user. Following is one-way, with no approval step.
3. **Home timeline:** tweets from the people I follow, newest first, paginated 20 at a time.
4. **User timeline:** one user's own tweets, newest first.
5. **Like and unlike** a tweet, with a like count shown on each tweet.

**Out of scope (say this out loud):** retweets and quote tweets, DMs, ads, trends and hashtag ranking, notifications, ranked or ML feeds (we use reverse-chronological order), and edit/delete beyond a soft delete. **Search** is a stretch goal, with no deep dive.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Home timeline latency | p99 < 200 ms | It is the product's main screen |
| Availability | 99.99% for reads | Availability beats consistency (AP): a stale feed is fine, an empty one is not |
| Freshness | A new tweet reaches followers' timelines within ~5 s | Eventual consistency is fine |
| Read-your-writes | My own tweet appears in my own timeline immediately | Otherwise users think the post failed |
| Durability | A posted tweet is never lost | It is acknowledged only after the DB write |
| Read:write ratio | ~20:1 (requests), ~400:1 (tweet rows read) | The design is read-optimised |

## Capacity estimate (high level)
**Facts:** 200M DAU; 10 timeline loads/day (20 tweets each); 0.5 tweets/day per user (10% with ~500 KB media); 5 likes/day; 200 followers on average, celebrities up to 100M; tweet row ~500 B; peak = 3× average; 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Timeline reads/s | 200M × 10 ÷ 10⁵ | **20k/s avg, 60k/s peak** | ~30 Timeline Service instances; reads come from Redis |
| Tweets/s | 100M ÷ 10⁵ | **1k/s avg, 3k/s peak** | Writes are light |
| Fan-out writes/s | 100M × 200 followers | **20B/day → 600k/s peak** | Timelines live in Redis, not SQL; skip celebrities (hybrid) |
| Likes/s | 200M × 5 ÷ 10⁵ | **10k/s avg, 30k/s peak** | Redis counters; split counters for viral tweets |
| Timeline cache | 200M × 800 ids × 8 B × 2 | **~2.6 TB** | ~52 Redis primaries (or keep 200 ids → ~13) |
| Tweet storage | 100M × 500 B | **50 GB/day → ~90 TB in 5 years** | Cassandra, sized by storage (~95% of reads hit the cache) |
| Media | 10M × 500 KB | **~5 TB/day** | Blob + CDN with SAS upload |

**What the numbers tell you:** reads dominate, so build timelines in advance (fan-out on write). A celebrity tweet would mean 50M+ inserts, so use a **hybrid**: push for normal users, pull for celebrities. Memory (2.6 TB of timelines) sets the Redis size; storage sets the Tweets store size.

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /tweets` | `{ text (≤ 280), mediaIds? }` | `201 { tweetId, createdAt }` | Idempotency key header; outbox → fan-out |
| `POST /media/upload-url` | `{ contentType, size }` | `200 { mediaId, sasUrl }` | Upload straight to Blob |
| `POST /follows` · `DELETE /follows/{userId}` | `{ userId }` | `204` | Follow is one-way, no approval |
| `POST /likes` · `DELETE /likes/{tweetId}` | `{ tweetId }` | `204` (`202` for viral tweets, counted async) | Insert-if-new, so a double like counts once |
| `GET /home?cursor=&limit=20` | — | `200 { tweets[], nextCursor }` | Push + pull merge; cursor = last tweet id (Snowflake) |
| `GET /users/{id}/tweets?cursor=&limit=20` | — | `200 { tweets[], nextCursor }` | Profile timeline |

- Each tweet in a response carries `{ tweetId, author, text, mediaUrls, likeCount, likedByMe, createdAt }`.
- Pagination is **cursor-based** (`max_id`), not offset, so new tweets don't shift pages (M14).

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

## Which components each path touches
| Requirement | Path | Regular user (row A) | Celebrity (row B) |
|---|---|---|---|
| Post a tweet | Write | Tweet Service → Tweets store + outbox → Kafka → fan-out worker → **Timeline cache of all 200 followers** | Same up to the fan-out worker, which **skips** the 50M timelines and writes only to the **celebrity recent cache** |
| Media | Write / Read | Blob storage (upload with a SAS URL) / CDN | Same; the CDN absorbs the spike |
| Follow / unfollow | Write | Graph Service → Graph store + outbox → Kafka → graph worker → reverse edge + **backfill** of the followed user's last 20 tweets into my timeline cache (`LREM` on unfollow) | Same up to the graph worker → **no backfill**; only `SADD`/`SREM` on my celebs set and an update of the follower count |
| Like / unlike | Write | Like Service → Likes store (insert if the row is new) → like counter `INCR` (synchronous) | Like Service → **split counter** (1 of 16 shards) + Kafka → aggregator → Likes store in batches (async, 202) |
| Home timeline | Read | Timeline Service → Timeline cache (20 ids) → Tweet cache + like counter → Tweets store only on a miss | Timeline Service → Timeline cache + celebs set + celebrity recent cache (in parallel) → **merge** → hydrate |
| Profile timeline | Read | Timeline Service → `user_tweets` → Tweet cache | Timeline Service → in-process cache → celebrity recent cache → Tweet cache (never reaches Cassandra) |

**What the read path never touches:** Kafka, the workers, the Graph store, or the Likes store. Reads come almost entirely from Redis. The Tweets store is hit only on a cache miss.

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
