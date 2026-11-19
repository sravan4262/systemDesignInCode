# Case 2: WhatsApp (chat)

Diagram: [case-02-whatsapp.excalidraw](case-02-whatsapp.excalidraw), in two rows: **A. 1:1 chat** and **B. Group chat (256 members)**. In each row: User → Connection Gateway (WebSocket), which then splits into a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

> **Status:** the learner did requirements live and stopped at the estimates (2026-11-18). If you want to do the estimates yourself first, skip the "Capacity estimate" section and come back to it.

## Prompt (what the interviewer says)
> "Design a messaging app like WhatsApp. Users can send messages to each other in one-to-one chats and in group chats."

## Functional requirements (in scope)
1. **Send messages** in 1:1 chats and in groups of up to **256 members**.
2. **Text and media** (images, video, audio up to 100 MB). Media goes to Blob Storage, and the message carries only a reference.
3. **Receipts:** sent ✓ (the server has it), delivered ✓✓ (the device has it), read (blue).
4. **Offline delivery:** messages to offline users are kept and delivered when they reconnect, with a push notification in the meantime.
5. **History:** load older messages when scrolling up (paged).

**Out of scope (say this out loud):** voice/video calls, broadcast lists, "last seen", end-to-end encryption, multiple devices per user, stories, search.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Delivery latency | < 500 ms end to end when both users are online, globally | It must feel like a conversation |
| Availability | Sending and receiving always work (AP) | A chat app that's down is unusable |
| Durability | Once the sender sees ✓, the message is never lost | ✓ is sent only after the store write |
| Ordering | Same order for everyone within a chat | A sequence number per chat, never timestamps (M17) |
| Delivery semantics | At least once + dedupe by (chat, seq) on the device | Exactly-once is not possible over a network |
| Geography | Global (India, Brazil, Europe, US) | Gateways in many regions |

## Capacity estimate
### 0. Facts (agree these with the interviewer first)
| Fact | Value |
|---|---|
| Users | 2B registered, **500M DAU** |
| Messages | 40 per user per day → **20B/day** |
| Mix | 90% text (~100 B), 10% media (~200 KB) |
| Groups | Assume 30% of messages are in groups, average group size 20 |
| Online at peak | 20% of DAU → **100M concurrent connections** |
| Peak / retention | Peak = 3× average. The server keeps messages **30 days**; history lives on the device (see deep dive 1) |

Conversion: 1 day ≈ 10⁵ s, so 20B/day ≈ 200k/s.

### 1. API estimate → number of servers
| Service | Per day | Average | Peak | Instances at peak |
|---|---|---|---|---|
| Connection gateways | 100M open WebSockets | — | 100M | ÷ ~200k per node = 500 → **~600** (headroom), across regions |
| Chat Service (send) | 20B | 200k/s | **600k/s** | ÷ ~5k/s each (INCR + write + publish) = **120** |
| Delivery workers | 20B × (0.7 × 1 + 0.3 × 19) ≈ 128B deliveries | 1.3M/s | **~3.8M/s** | ÷ ~50k/s each (batched MGET + pushes) = **~80** |
| Receipt Service | batched "up to seq" per chat | ~170k/s | ~500k/s | ÷ ~10k/s each = **50** |
| Sync Service (reconnect + history) | 500M × ~10 reconnects = 5B | 50k/s | 150k/s | ÷ ~2k/s each = **75** |
| **Total** | | | | **~925 instances** (gateways dominate) |

### 2. Redis estimate → number of nodes
| Cluster | What it holds | Memory | Ops at peak | Primaries | Nodes with replicas |
|---|---|---|---|---|---|
| Presence | user → gateway node (TTL, refreshed every ~60 s) | 100M × 100 B × 2 = **20 GB** | Lookups ~1M/s (MGET batches) + refreshes ~1.7M/s | Ops decide: ~3M ÷ 100k = **30** | **60** |
| Seq counters | `chat:{id}` → last seq | Chats active in 30 days ~10B × 60 B × 1.5 = **900 GB** | 600k INCR/s + reconnect MGETs | Memory decides: 900 ÷ 50 = **18** | **36** (AOF on: a seq must never go backwards) |
| Recent messages | Last 20 messages for chats active in the last hour (~200M chats) | 200M × 20 × 150 B × 1.5 ≈ **900 GB** | Sync reads ~150k/s | Memory decides: **18** | **36** |
| Group members | `group:{id}:members` (≤ 256 ids) | ~500M groups × 20 × 8 B × 2 ≈ **160 GB** | Delivery ~180k/s | **4** | **8** |
| **Total** | | | | | **~140 nodes** |

### 3. Database estimate → number of nodes
Rule of thumb: ~2 TB of data per node, **replication factor 3** across 3 availability zones.

| Store | Key | Data | × RF 3 | Load at peak | Servers |
|---|---|---|---|---|---|
| Messages (Cassandra, or Cosmos) | pk `(chat_id, month)`, ck `seq` | 20B × ~150 B (text + metadata) = 3 TB/day → 30 days = **90 TB** | 270 TB | 600k writes/s | 270 ÷ 2 TB = **135 nodes** (storage decides; at 1-year retention it would be ~1,650) |
| user_chats (membership) | pk `user_id` → chat ids | 2B × 50 chats × 40 B = **4 TB** | 12 TB | Changes only on join/leave | **6** |
| Receipts | pk `(chat_id)`, ck `user_id` → delivered_seq, read_seq | **~4 TB** | 12 TB | ~500k upserts/s | Writes decide: **~60** |
| Kafka | `message-sent`, `receipt-events`, 256 partitions by chat_id | ~230 MB/s, 3-day retention ≈ 60 TB | 180 TB | — | **~20 brokers** |
| Media (Blob + CDN) | `media/{chat}/{id}` | 2B × 200 KB = **400 TB/day** → 30 days ≈ 12 PB | Managed | Served by the CDN | Managed |
| **Total (excluding managed)** | | | | | **~220 servers** |

### What the numbers tell you
- **100M open connections** is the defining number. You need a dedicated gateway layer that holds sockets and does nothing else. (Azure SignalR Service tops out far below this, so these are self-run gateways.)
- 3.8M deliveries/s means you **don't write anything per recipient**. Store each message once, keep one seq counter per chat, and push.
- Message storage is sized by **retention**: 30 days on the server is ~135 nodes; a year of history would be ~10×. That's why history lives on the device.
- Media volume (400 TB/day) is 100× the text, so it must never touch the chat path: SAS upload plus the CDN (claim-check).

## High-level design (see the diagram)
**Send (write path):**
1. The app keeps one WebSocket to a Connection Gateway, which registers `user → gateway node` in Presence.
2. Gateway → Chat Service: `seq = INCR chat:{id}`, write `(chat_id, seq, sender, body or media ref)` to the Messages store, then ack ✓ to the sender.
3. Publish `message-sent` to Kafka, partitioned by chat_id, so each chat's order is kept.
4. The Delivery worker looks up each recipient in Presence:
   - **online:** push through their gateway node;
   - **offline:** send a push notification through Notification Hubs (APNs/FCM).
   - **Groups:** read the members set, one Presence MGET for all members, one push per gateway node, and one collapsed notification for offline members.

**Receipts:** the device acks "delivered up to seq N" or "read up to seq N" for the chat. The Receipt Service upserts that into the Receipts store and publishes an event, which is pushed to the sender's gateway to update the ticks. In a group, blue ticks appear when **every member's read_seq ≥ N**.

**Reconnect and history (read path):**
1. The app reconnects and sends its last seq per chat.
2. The Sync Service reads `user_chats` (which chats I'm in), then MGETs the seq counters (which chats changed).
3. For each changed chat it returns the messages with seq greater than the client's: from the recent-messages cache, or the Messages store on a miss.
4. Scrolling up pages older messages by seq.

**Media:** upload straight to Blob with a SAS URL; the message carries the blob reference; recipients download from the CDN.

## Which components each path touches
| Requirement | Path | 1:1 chat (row A) | Group chat (row B) |
|---|---|---|---|
| Send | Write | Gateway → Chat Service → seq INCR → Messages store → ✓ → Kafka → Delivery worker → Presence → recipient's gateway **or** Notification Hubs | Same, but stored once; the worker reads the members set, does one Presence MGET for 255 users, one push per gateway node, one collapsed notification |
| Receipts | Write | Gateway → Receipt Service → Receipts store → Kafka → sender's gateway | Same per member; blue when min(read_seq) ≥ N |
| Offline / reconnect | Read | Sync Service → user_chats → seq counters (MGET) → recent cache → Messages store on a miss | Same; the group's recent cache is shared by all members (hot) |
| History | Read | Sync Service → Messages store by `(chat_id, month)`, seq range | Same |
| Media | Write / Read | Blob (SAS upload) / CDN | Same; one blob, many downloads |

**What the live send path never touches:** the Sync Service, `user_chats`, or the Receipts store. Online delivery is a push.

## Deep dives to drive (learner leads, interviewer pushes)
1. **Retention:** keep everything on the server (search, multi-device, ~10× storage) vs. store-and-forward with a short server retention (what WhatsApp does). What breaks if a user loses their phone?
2. **Ordering:** a seq from one counter per chat vs. timestamps (clock skew, M17). What if the Redis counter is lost? Use AOF + replica, or recover from max(seq) in the store, and never reuse a seq.
3. **At-least-once:** the gateway can push a message twice (a retry after a lost ack). The device dedupes by (chat, seq); acks are cumulative ("up to N").
4. **Gateway failure:** 200k users drop at once. Clients reconnect with jitter to another node, re-register Presence, and sync by seq. Nothing is lost because the store is the source of truth.
5. **Big groups:** at 256 members fan-out is cheap. What changes at 100k members (a channel)? Readers pull, like Twitter's celebrity case.
6. **Hot chat:** one very busy group puts everything on one Kafka partition and one counter. It's fine at a few hundred messages/s; beyond that, split the chat into partitioned sub-streams.

## Interview follow-ups (answer these after the design)
- Bob was offline for 3 days in 40 chats. Walk through exactly what happens when he opens the app.
- How does Alice's phone know to show blue ticks in a group of 100?
- Why not let the Chat Service push straight to the recipient instead of going through Kafka?
