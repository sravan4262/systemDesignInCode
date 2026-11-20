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

## Capacity estimate (high level)
**Facts:** 500M DAU × 40 messages/day = **20B messages/day**; 90% text (~100 B), 10% media (~200 KB); 20% of DAU online at peak; peak = 3× average; 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Messages/s | 20B ÷ 10⁵ | **200k/s avg, 600k/s peak** | ~120 Chat Service instances; Kafka partitioned by chat |
| Deliveries/s | groups fan out ~6× | **~4M pushes/s peak** | Store each message once; push, don't write per recipient |
| Open connections | 20% × 500M | **100M WebSockets** | Dedicated gateway layer, ~500 nodes at ~200k each |
| Text storage | 18B × ~150 B | **~3 TB/day** | 30 days on the server ≈ 90 TB (~135 Cassandra nodes); history lives on the device |
| Media storage | 2B × 200 KB | **~400 TB/day** | Blob + CDN with SAS upload; never through the chat path |

**What the numbers tell you:** 100M open connections and ~4M pushes/s shape the design (gateway layer, push model, one stored copy per message). Media volume is 100× the text, so it goes straight to Blob storage. Server storage is set by **retention**, which is why messages are kept only 30 days on the server.

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
