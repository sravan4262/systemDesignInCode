# Case 4: YouTube (upload, search, subscribe, watch, like, comment)

Diagram: [case-04-youtube.excalidraw](case-04-youtube.excalidraw), in two rows: **A. Regular creator** and **B. Hot creator** (100M subscribers; millions of views, likes and comments right after an upload). In each row: User → API Gateway (drawn as one tall box; every service hangs off it), which then splits into a **write path** (top) and a **read path** (bottom). The video bytes never go through the gateway: uploads go straight to Blob storage, and playback streams from the CDN. To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design YouTube. Users can upload videos and other users can watch them."

## Functional requirements (in scope)
1. **Upload a video** (up to ~10 GB) with a title, description and tags. Uploads are resumable; the video is processed into several qualities before it can be watched.
2. **Search** videos by title, description and tags.
3. **Subscribe / unsubscribe** to a channel, and see a **subscriptions feed** (latest videos from my channels, newest first).
4. **Watch a video** with adaptive quality (the player switches resolution as the network changes).
5. **Like / unlike** a video and **comment** on it; like, comment and subscriber counts are shown.

**Must handle both:** a regular creator (a few thousand views, likes trickle in) and a **hot creator** (100M subscribers; 10M views, 1M likes and 100k comments in the first hour after an upload).

**Out of scope (say this out loud):** recommendations and the home feed, notifications ("new video from…"), comment replies and comment likes, live streaming, ads, DRM, copyright matching, view-count accuracy (shown as approximate).

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Playback start | < 2 s to first frame; no buffering | Viewers leave after a few seconds of waiting |
| Availability | Watch, search, like and comment always work (AP) | A stale count is fine; a video that won't play is not |
| Upload durability | An uploaded video is never lost | The raw file is kept until processing succeeds |
| Processing time | Minutes | The creator sees "processing" |
| Freshness | New video in search and subscriber feeds within ~1 min; counts lag ≤ a few s | Eventual consistency is fine |
| Read-your-writes | My own like / comment shows immediately | Otherwise users think it failed |
| Correctness | One like per user per video | Insert-if-new on `(userId, videoId)` |
| Geography | Global; a viewer in India never crosses an ocean on the common path | CDN POPs, Cosmos read copies, **regional Blob copies** for CDN misses, Redis per region |

## Capacity estimate (high level)
**Facts:** 500M DAU; 5 watches, 2 searches, 1 like/day each; 1 comment per 10 users/day; 1M uploads/day (10 min, ~500 MB raw); avg watch 5 min at ~2.5 Mbps; hot creator: 10M views / 1M likes / 100k comments in the first hour; peak = 3× average; 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Watch-page reads/s | 2.5B ÷ 10⁵ | **25k/s avg, 75k/s peak** | Redis in front of Cosmos |
| Searches/s | 1B ÷ 10⁵ | **10k/s avg, 30k/s peak** | AI Search with replicas; cache popular queries |
| Likes + comments/s | (500M + 50M) ÷ 10⁵ | **~5.5k/s avg, ~17k/s peak** | Cheap overall; the problem is **one hot video** |
| Hot video, first hour | 1M likes, 100k comments ÷ 3,600 s | **~300 likes/s (bursts 2–3k/s), ~30 comments/s; 3k watch-page reads/s** | One count row and one comments partition get hammered → aggregate counts, bucket comments, cache page 1 |
| Uploads + storage | 1M × 500 MB raw + ~1.5× processed | **~10 uploads/s; ~1.2 PB/day** | Direct-to-Blob upload; raw to cold tier after processing |
| Transcode compute | 10M video-min × 5 renditions | **~35k cores busy** | 4-s chunks in parallel; autoscale on queue lag; spot VMs |
| Egress | ~25M concurrent viewers × 2.5 Mbps | **~60 Tbps peak** | CDN only; our services never touch the bytes |

**What the numbers tell you:** video is a **bandwidth and storage** problem (CDN, Blob tiers). The request rates are ordinary *on average*; what breaks is **concentration**: one hot video turns into one hot count row, one hot comments partition and one hot metadata key. And a hot creator's upload must **not** fan out to 100M subscriber feeds, so the subscriptions feed is a **pull**.

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /videos` | `{ title, description, tags[], sizeBytes, contentType }` | `201 { videoId, uploadUrl (SAS), expiresAt }` | Creates the metadata row (`status = uploading`) |
| *(direct to Blob)* `PUT {uploadUrl}&comp=block&blockid=…` | 4–100 MB block | `201` | Resumable: re-send only missing blocks |
| `POST /videos/{id}/complete` | `{ blockIds[] }` | `202 { status: processing }` | Idempotent; outbox → `video-uploaded` |
| `GET /videos/{id}` | — | `200 { videoId, title, channel, status, manifestUrl, thumbnailUrl, likeCount, commentCount, likedByMe, subscribedToChannel }` | The player then reads the manifest + segments from the **CDN** |
| `GET /search?q=&cursor=` | — | `200 { videos[], nextCursor }` | Ranked by text relevance + popularity |
| `POST /subscriptions` · `DELETE /subscriptions/{channelId}` | `{ channelId }` | `204` | Insert-if-new, so a double subscribe counts once |
| `GET /feed/subscriptions?cursor=` | — | `200 { videos[], nextCursor }` | Pull: merge my channels' latest lists |
| `POST /videos/{id}/likes` · `DELETE /videos/{id}/likes` | — | `204` (`202` on hot videos, counted async) | Insert-if-new on `(userId, videoId)` |
| `POST /videos/{id}/comments` | `{ text (≤ 10k) }` | `201 { commentId, createdAt }` (`202` on hot videos) | Idempotency key header |
| `GET /videos/{id}/comments?cursor=&limit=20` | — | `200 { comments[], nextCursor }` | Page 1 from cache; cursor = commentId (time-sortable) |

- Pagination is **cursor-based** everywhere (time-sortable ids), never offset.
- The client shows my own like or comment immediately (optimistic UI), and the API returns it; the public count catches up a few seconds later.

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| Upload Service | Video row + SAS URL; on `/complete`, commits the blocks and writes an outbox row | **Video metadata: Cosmos DB** (pk `videoId`) |
| Transcode orchestrator + workers | Split into 4-s chunks; one job per chunk × rendition; then manifest + `status = ready` | **Durable Functions** + containers scaled by **KEDA**; output to **Blob: processed** in the home region, then **async object replication** to regional copies (West Europe, Central India, Japan East); these are the CDN origins |
| Search indexer → Search Service | On `video-ready`, upsert the video into the index; counts refreshed every few minutes for ranking | **Azure AI Search** (replicas for read throughput) |
| Subscribe Service | Subscribe / unsubscribe | **Subscriptions: Cosmos**, pk `userId` (my channels); subscriber count in the Counts store |
| Channel feed updater → Feed Service | On `video-ready`, `LPUSH` the id onto that channel's list; the feed merges my channels' lists at read time | **Redis** `channel:{id}` (last 20 ids) |
| Like Service | Like / unlike | **Likes: Cosmos**, pk `userId`, id `videoId` (insert if new) |
| Comment Service | Write and page comments | **Comments: Cosmos**, pk `videoId` (hot videos: `(videoId, hour)`); page-1 cache in **Redis** |
| Counts | Like / comment / subscriber counts | **Cosmos** (durable) + **Redis** (what the watch page reads) |
| Video Service | Watch page: metadata, counts, my like / subscribe state | **Redis (per region)** → nearest Cosmos read copy on a miss |
| Kafka | `video-uploaded`, `video-ready`, `count-events` | Azure Event Hubs (Kafka API) |
| CDN | Manifests + segments | **Azure Front Door** (edge POPs + origin shield) |

**Write path:**
1. **Upload:** Upload Service → metadata (`uploading`) → SAS URL → client uploads blocks straight to Blob → `/complete` → `processing` + outbox → Kafka `video-uploaded` → orchestrator → workers write HLS segments → manifest, `status = ready` → outbox → Kafka `video-ready`.
2. **`video-ready` has two consumers:** the search indexer (upsert into AI Search) and the channel feed updater (`LPUSH channel:{id}`, `LTRIM` 20). **Nothing is written per subscriber.**
3. **Subscribe / like / comment (regular video):** write one row, then `+1` on the count (Cosmos patch + Redis `INCR`). A few per second on one video is nothing.
4. **Hot video:** the row writes stay the same (likes and subscriptions are keyed by `userId`, so 1M likes spread over all partitions). Only the **count** is hot, so the service emits a `count-event` instead and returns `202`; the count aggregator sums per video per second and writes **one** update per second.

**Read path:**
1. **Watch:** Video Service (in the viewer's region) → Redis (`video:{id}` + counts) → nearest Cosmos read copy on a miss → returns the manifest URL. The player streams segments from the CDN: edge → origin shield → **nearest regional Blob copy** → home region only if the copy isn't there yet (a brand-new or long-tail video).
2. **Search:** Search Service → AI Search (hot: popular-query cache in Redis, TTL 30 s).
3. **Comments:** page 1 from the Redis cache, older pages from Cosmos by cursor.
4. **Subscriptions feed (pull):** my ~50 channels from Subscriptions → `MGET` 50 channel lists from Redis → merge by time → hydrate from the metadata cache.

## Which components each path touches
| Requirement | Path | Regular creator (row A) | Hot creator (row B) |
|---|---|---|---|
| Upload | Write | Upload Service → metadata → raw Blob (SAS) → Kafka → orchestrator → workers → processed Blob → `video-ready` → search indexer + channel feed updater | Same, plus a **priority lane** in the transcode queue and a **pre-warm job** that pushes the first segments to CDN POPs; **no fan-out** to 100M subscribers |
| Subscribe | Write | Subscribe Service → Subscriptions (pk `userId`) → count +1 | Same row write; the count goes **Kafka → aggregator → 1 write/s** |
| Like | Write | Like Service → Likes (insert if new) → count +1 (sync) | Same row write (spread by `userId`); count async via aggregator, `202` |
| Comment | Write | Comment Service → Comments (pk `videoId`) → count +1 | Comments pk `(videoId, hour)` so one partition doesn't take every write; count via aggregator, `202` |
| Watch | Read | Video Service → Redis → nearest Cosmos read copy on a miss; segments CDN → nearest regional Blob copy → home region if not copied yet | **In-process cache (5 s)** → Redis replicas; segments CDN edge (~99%) → **origin shield** → Blob |
| Search | Read | Search Service → AI Search | **Popular-query cache** → AI Search replicas |
| Comments | Read | Page-1 cache → Cosmos | **Top-comments cache rebuilt every 5 s** (not on every write) → Cosmos for older pages |
| Subscriptions feed | Read | Feed Service → my channels → channel lists (Redis) | Same; the hot channel's list is read by millions → Redis replicas + in-process cache |

**Both kinds of data are read in-region:** metadata from Cosmos read copies (built in), video bytes from regional Blob copies (object replication). Only a CDN miss for a video that isn't copied yet crosses regions.

**What the read path never touches:** Kafka, the orchestrator, workers, indexer or aggregator. **What our services never touch:** the video bytes.

## Deep dives to drive (learner leads, interviewer pushes)
1. **Subscriptions feed: push vs. pull.** Push (Twitter's fan-out on write) would mean 100M writes per upload for a hot creator. Pull works here because a user follows ~50 channels and channels upload rarely, so the read is 50 small Redis lists. Compare with Case 3, where the hybrid was needed.
2. **Hot counts.** Why key likes by `userId` instead of `videoId`? (The "who liked" rows spread out; "did I like it?" is a point read.) Then only the count is hot: aggregate it (Kafka → 1 write/s) or use split counters. An approximate count is fine.
3. **Hot comments.** 30–3k writes/s and millions of page-1 reads on one video. Bucket the partition key by hour; rebuild the top-comments cache every few seconds instead of invalidating on every write; spam/moderation runs async.
4. **Search freshness vs. cost.** Index on `video-ready` (seconds), but refresh like/view counts for ranking in batches every few minutes; per-like index updates would melt the index.
5. **Chunked transcoding.** Minutes instead of hours; retry one chunk; idempotent output paths `{videoId}/{rendition}/seg_{n}.ts`. Why chunk boundaries must be keyframes.
6. **Regional copies of the video bytes.** Copying every rendition of every video to 4 regions quadruples ~750 TB/day. Copy **popular and recent** videos (and the low renditions first); the long tail stays in the home region, where a rare CDN miss crossing the ocean is acceptable. Compare Cosmos, where every metadata row is copied because rows are tiny.
7. **CDN for a hot upload.** 10M viewers in an hour hit the same first segments. Pre-warm + origin shield so the first wave of misses doesn't stampede Blob.
8. **Unsubscribe / unlike races.** A double click sends like then unlike out of order. Use a per-row version assigned by the server (never client timestamps: clock skew, M17), and the count changes only when the row's state changes, not per request.

## Interview follow-ups (answer these after the design)
- A creator with 100M subscribers uploads. Walk through what happens before the first subscriber sees it in their feed, and why nothing is written per subscriber.
- 2k likes/s hit one video. Which component would melt first in the regular design, and what changes?
- A user likes a video and refreshes: the count hasn't moved. Is that a bug? How do you make it look right to them?
