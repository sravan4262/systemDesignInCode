# Case 4: YouTube (video upload + streaming)

Diagram: [case-04-youtube.excalidraw](case-04-youtube.excalidraw), in two rows: **A. Regular video** and **B. Viral video**. In each row: User → API Gateway, which then splits into a **write path** (top) and a **read path** (bottom). The video bytes never go through the gateway: uploads go straight to Blob storage, and playback streams from the CDN. To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design YouTube. Users can upload videos and other users can watch them."

## Functional requirements (in scope)
1. **Upload a video** (up to ~10 GB) with a title and description. Uploads are resumable.
2. **Process it** into several qualities (240p → 1080p) plus a thumbnail. The video becomes watchable when processing finishes.
3. **Watch a video** with **adaptive quality**: the player switches resolution as the network changes.
4. **View count** shown on each video.
5. **Channel page:** a creator's videos, newest first.

**Out of scope (say this out loud):** recommendations and the home feed, search (a stretch goal), comments and likes, live streaming, subscriptions and notifications, ads and monetization, DRM, and copyright matching.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Playback start | < 2 s to first frame; no buffering | Viewers leave after a few seconds of waiting |
| Availability | Watching always works (AP) | A stale view count is fine; a video that won't play is not |
| Upload durability | An uploaded video is never lost | The raw file is kept until processing succeeds (and longer, in cold storage) |
| Processing time | Minutes, not seconds | The creator sees "processing"; that's acceptable |
| View count | Eventual, approximate (~1 min lag) | Nobody needs an exact count in real time |
| Geography | Global viewers | Segments are served from CDN POPs near the viewer |
| Read:write ratio | ~2,500 views per upload | The design is read-optimised, and egress is the main cost |

## Capacity estimate (high level)
**Facts:** 500M DAU × 5 videos watched/day; 1M uploads/day, 10 min each, ~500 MB raw; average watch 5 min at ~2.5 Mbps (720p); peak = 3× average; 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Uploads/s | 1M ÷ 10⁵ | **~10/s avg, 30/s peak** | Writes are light; the work is in processing |
| Watch-page reads/s | 2.5B ÷ 10⁵ | **25k/s avg, 75k/s peak** | Metadata from Redis; ~40 Video Service instances |
| Raw storage | 1M × 500 MB | **~500 TB/day** | Move raw files to cold/archive tier after processing |
| Processed storage | ~1.5× raw (5 renditions) | **~750 TB/day** | Blob storage; this sets the storage bill |
| Transcode compute | 10M video-min × 5 renditions | **~35k cores busy** | Split into 4-s chunks, run in parallel; autoscale on queue lag; spot VMs |
| Concurrent viewers | 2.5B × 5 min ÷ 1,440 min | **~9M avg, ~25M peak** | — |
| Egress | 25M × 2.5 Mbps | **~60 Tbps peak** | Only a CDN can serve this; our services never touch the bytes |

**What the numbers tell you:** this is a **bandwidth and storage** problem, not a request-rate problem. 75k metadata reads/s is ordinary; 60 Tbps of video is not, so every byte must come from the CDN. Storage grows by ~1 PB/day, which is why raw files go to cheap tiers. Transcoding is heavy but async, so it scales with parallel workers, not faster machines.

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /videos` | `{ title, description, sizeBytes, contentType }` | `201 { videoId, uploadUrl (SAS), expiresAt }` | Creates the metadata row (`status = uploading`) |
| *(direct to Blob)* `PUT {uploadUrl}&comp=block&blockid=…` | 4–100 MB block | `201` | Resumable: re-send only missing blocks |
| `POST /videos/{id}/complete` | `{ blockIds[] }` | `202 { status: processing }` | Commits the blocks; outbox → `video-uploaded` |
| `GET /videos/{id}` | — | `200 { videoId, title, status, manifestUrl, thumbnailUrl, viewCount, durationSec }` | `404` if missing; `status = processing` until ready |
| *(CDN)* `GET {manifestUrl}` → `GET …/720p/seg_0042.ts` | — | HLS manifest, then 4-s segments | The player picks the quality (ABR) |
| `POST /videos/{id}/views` | `{ watchedSec }` | `202` | Beacon; counted once the viewer passes ~30 s |
| `GET /channels/{id}/videos?cursor=&limit=20` | — | `200 { videos[], nextCursor }` | Cursor = last `createdAt + videoId` |

- `POST /complete` is **idempotent**: calling it twice for the same `videoId` starts processing only once (the status moves `uploading → processing` with an optimistic-concurrency check).
- The SAS URL is write-only, for this one blob, and expires in a few hours. The client can ask for a new one to resume.

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| Upload Service | Creates the video row and the SAS URL; on `/complete`, commits the block list and writes an outbox row | **Video metadata: Cosmos DB** (pk `videoId`; a second container `channel_videos` with pk `channelId` for the channel page) |
| Blob: raw | Holds the uploaded original | **Azure Blob**, staged blocks; moved to cold/archive after processing |
| Transcode orchestrator | Splits the video into 4-s chunks, fans out one job per chunk × rendition, waits for all, then finalizes | **Durable Functions** (fan-out / fan-in), triggered from Kafka `video-uploaded` |
| Transcode workers | Read one raw chunk, encode it to one rendition, write the segment | Containers on AKS / Container Apps, scaled by **KEDA** on queue lag, spot VMs |
| Blob: processed | HLS segments per rendition + manifests + thumbnails | **Azure Blob**, the **CDN origin** |
| Video Service | Watch-page metadata and the channel page (read only) | **Redis** (`video:{id}`), Cosmos read copy on a miss |
| View Service + views consumer | Takes view beacons; counts per video per minute | Kafka topic `views` → consumer group → **view counts (Cosmos)** |
| CDN | Serves manifests and segments to viewers | **Azure Front Door** (edge POPs + origin shield) |

**Write path (upload → watchable):**
1. Client → Gateway → Upload Service `POST /videos`: it writes the metadata row (`uploading`) and returns a SAS URL.
2. The client uploads the file **straight to Blob** in blocks (staged blocks; resumable). Our services never carry the bytes.
3. The client calls `/complete`. The Upload Service commits the block list, sets `processing`, and writes the outbox row in the same write → Kafka `video-uploaded`.
4. The orchestrator splits the video into 4-s chunks and fans out **chunk × rendition** jobs (e.g. 150 chunks × 5 = 750 jobs). Workers run them in parallel and write HLS segments to processed Blob.
5. When every job is done, the finalize step writes the manifests (`master.m3u8` → one per quality) and the thumbnail, then sets `status = ready`.
6. Each job is **idempotent**: its output path is `{videoId}/{rendition}/seg_{n}.ts`, so a retried job overwrites the same file.

**Read path (watch):**
1. Client → Gateway → Video Service `GET /videos/{id}` → Redis → Cosmos read copy on a miss. It returns the title, view count and **manifest URL**.
2. The player fetches the manifest from the CDN, then segments one by one. It measures its download speed and switches quality between segments (**adaptive bitrate**).
3. CDN miss → origin shield → processed Blob. Popular segments stay at the edge; long-tail videos come from the origin.

**View counts:** the player sends a beacon after ~30 s of watching → View Service → Kafka `views` → views consumer sums per video per minute → one write per video per minute to the view counts table. The watch page shows the cached, approximate count.

## Which components each path touches
| Requirement | Path | Regular video (row A) | Viral video (row B) |
|---|---|---|---|
| Upload | Write | Upload Service → metadata (Cosmos) → raw Blob (direct, SAS) → `/complete` → outbox → Kafka | Same |
| Processing | Write | Orchestrator → transcode workers (chunk × rendition) → processed Blob → finalize (manifest, `ready`) | Same |
| View count | Write | View Service → Kafka `views` → consumer → view counts (1 write / min) | View Service **sums in memory and sends every 5 s**, so one hot video isn't 3k messages/s; the consumer flags trending videos and **pre-warms CDN POPs** |
| Watch page | Read | Video Service → Redis → Cosmos read copy on a miss | Video Service **in-process cache (5 s)** → Redis (replicas) → Cosmos almost never |
| Playback | Read | CDN → origin (processed Blob) on a miss | CDN edge (~99% hits) → **origin shield** collapses misses → Blob rarely hit |
| Channel page | Read | Video Service → `channel_videos` (pk `channelId`) | Same |

**What the read path never touches:** the Upload Service, Kafka, the orchestrator or the workers. **What our services never touch:** the video bytes (Blob and the CDN carry them).

## Deep dives to drive (learner leads, interviewer pushes)
1. **Why chunk the transcode?** One 2-hour video on one machine takes hours; 1,800 chunks on 1,800 workers take minutes. A failed chunk is retried alone. The cost: chunk boundaries must fall on keyframes.
2. **Adaptive bitrate (HLS / DASH):** why short segments (2–6 s)? The trade-off: shorter = faster quality switches and faster start, but more requests and worse compression.
3. **CDN cost and the long tail:** most views go to a few videos (edge hits); most videos get almost no views (served from origin). Do you pre-encode every video in 4K? No: encode the top renditions only when a video gets popular.
4. **Resumable upload:** staged blocks + commit list (M19 pattern 6). What happens if the client disappears mid-upload? Uncommitted blocks expire after 7 days; a cleanup job marks the row `abandoned`.
5. **Processing failures:** a worker crashes mid-chunk. The orchestrator retries that job only; the output path makes it idempotent. After N failures the video goes to `failed` and the creator is told.
6. **View counting at scale:** pre-aggregate at the service, then per minute in the consumer. Dedupe the same viewer replaying (a short-lived `(videoId, viewerId)` set in Redis). Fraud filtering is a separate async job.
7. **Storage tiers:** raw → cool after processing → archive after 30 days. Old, unwatched renditions can move to cool too, at the cost of a slower first segment.

## Interview follow-ups (answer these after the design)
- Walk through what happens between the creator pressing "Upload" and the first viewer pressing "Play". Which steps are synchronous?
- A video goes viral 10 minutes after upload. Which component hurts first, and what protects it?
- Why not stream the video through the Video Service so you can check permissions on every request? (Hint: signed CDN URLs / tokens.)
