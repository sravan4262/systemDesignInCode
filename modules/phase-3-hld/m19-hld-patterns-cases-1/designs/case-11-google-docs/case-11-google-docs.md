# Case 11: Google Docs (collaborative editing)

Diagram: [case-11-google-docs.excalidraw](case-11-google-docs.excalidraw), in three rows: **A. Edit lock** (one editor at a time; every path), **B. Real-time co-editing** (many editors, no locks; every path) and **C. Hot document** (1,000 open, ~50 editing; only what differs from B, numbered in order). Users → APIM (REST) and a **WebSocket gateway** (live editing) → a **write path** (top) and a **read path** (bottom). To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design Google Docs. Several people can edit the same document at the same time."

## Functional requirements (in scope; learner's list, 2026-11-21)
1. **Create and open** a document (text with basic formatting).
2. **Edit**, in two modes:
   - **Edit lock (row A):** the user clicks **Edit**; only they can edit, everyone else is read-only.
   - **Co-editing (rows B, C):** several people type anywhere at the same time; everyone sees the changes within ~1 s and **all copies end up identical**.
3. **Presence:** who's in the document and where their cursors are (co-editing).
4. **Version history:** view earlier versions and restore one.
5. **Sharing:** viewer or editor access per user.

**Out of scope (say this out loud):** offline editing, comments and suggestion mode, export (PDF / Word), search across documents, spreadsheets and slides, images and complex layout (tables, page breaks).

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Edit latency | Your own keystrokes: instant (applied locally); others' edits visible in < 1 s | It must feel like typing, not like saving |
| **Convergence** | All copies of a document end up identical, whatever the order edits arrive in | The defining requirement of co-editing |
| Durability | An acknowledged edit is never lost | The op is in the op log before the ack |
| Lock safety (row A) | Never two editors; a forgotten lock frees itself | Leases + fencing tokens |
| Availability | Open and read always work; live editing pauses briefly on an owner-node failover | A few seconds of "reconnecting" is acceptable |
| Scale | Millions of open docs, but each doc is small and has few editors | Shard everything by `docId` |

## Capacity estimate (high level)
**Facts:** 50M DAU; at peak 5M users have a doc open, 20% of them typing at any moment; the client sends an op batch every ~200 ms while typing (~5 msgs/s); an op is ~100 B; average doc ~50 KB; 1B docs; 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Open WebSockets | 5M at peak | **5M connections** | ~25 gateway nodes at ~200k each |
| Ops/s | 1M typists × 5 msgs/s | **~5M ops/s at peak** | The heaviest write; **sharded by docId**, each doc's ops on one owner node |
| Active docs | ~2M docs with someone typing | **~2M live sessions** | ~100 collab nodes at ~20k active docs each |
| Op log | 5M ops/s × 100 B | **~0.5 GB/s while busy** | Compact into snapshots; keep raw ops ~30 days |
| Document storage | 1B docs × 50 KB | **~50 TB** (+ snapshots for history) | Blob storage; versions are pointers to snapshots |
| Row A locks | a few per second per doc at most | **trivial** | One Redis key per locked doc |

**What the numbers tell you:** the total load is big (5M ops/s), but it splits perfectly by document: each doc has a handful of editors and a few ops per second. So the design is "**one owner per document**", sharded across many nodes, not one giant ordered stream. Row C is the exception: one doc, many people.

## API
**REST** (via APIM)

| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /docs` | `{ title }` | `201 { docId }` | Creator becomes owner |
| `GET /docs/{id}` | — | `200 { title, rev, snapshotUrl, opsSinceSnapshot[], role }` · `403` | Open = snapshot + tail |
| `PUT /docs/{id}/permissions` | `{ userId, role: viewer\|editor }` | `204` | Owner only |
| `GET /docs/{id}/versions` | — | `200 { versions: [{ rev, at, by }] }` | — |
| `POST /docs/{id}/versions/{rev}/restore` | — | `200 { rev }` | Restore = a **new** latest version, history is kept |
| **Row A:** `POST /docs/{id}/lock` | — | `200 { lockToken, expiresAt }` · `423 { holder }` | `lockToken` = fencing token |
| **Row A:** `POST /docs/{id}/lock/renew` · `DELETE /docs/{id}/lock` | `{ lockToken }` | `200` · `409` (not yours any more) | Renew every 30 s |
| **Row A:** `PUT /docs/{id}/content` | `{ content, baseVersion, lockToken }` | `200 { version }` · `409` (stale version) · `423` (lock lost) | Whole-document save |

**WebSocket** (rows B, C) `wss://collab.example.com/docs/{id}`

| Direction | Frame | Payload | Notes |
|---|---|---|---|
| Client → server | `op` | `{ clientOpId, baseRev, ops: [{ insert\|delete, pos, text? }] }` | Batched every ~200 ms while typing |
| Server → client | `ack` | `{ clientOpId, rev }` | Your op is durable and ordered |
| Server → client | `op` | `{ rev, userId, ops }` | Someone else's edit, already transformed |
| Both ways | `cursor` / `presence` | `{ userId, pos }` · `{ users[] }` | Ephemeral, never stored |

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| Document Service | Create, open, share, versions, restore; row A locks and saves | **Docs, ACL, versions (SQL)**; content / snapshots in **Blob** |
| Locks (row A) | One editor per doc, as a lease with a fencing token | **Redis** `SET lock:{doc} … NX EX 120` |
| WebSocket gateway | A pipe; sends all sockets of a doc to that doc's owner node | Like Uber / WhatsApp |
| Doc router | Which collab node owns which doc | **Redis** `doc → node`, a lease the owner renews |
| Collaboration Service | The doc's **owner**: orders edits, transforms them (OT), appends, acks, broadcasts | In-memory doc state per active doc |
| Op log | Every accepted op, in order | **Cosmos**, pk `docId`, `rev` ascending, `clientOpId` unique |
| Snapshotter | Applies ops to the last snapshot every 500 ops / 1 min | Writes **snapshots to Blob** |
| Connections + presence | Which user is connected on which **gateway node** (and their cursor), per doc; used by the owner to send updates | **Redis** hash `conns:{docId}` (connId → user, gateway node, cursor), expiry refreshed by heartbeats; the owner keeps a copy in memory |
| Pub/sub (row C) | Fan a batch out to many gateway nodes | **Azure Web PubSub** or Redis pub/sub |

### Row A: edit lock
1. **Lock:** `SET lock:{doc} {user, token} NX EX 120`, where `token = INCR lockToken:{doc}` (the **fencing token**, always growing). Taken → `423 "Priya is editing"`, read-only.
2. **Lease:** the editor's app renews every 30 s (only if the lock is still theirs: compare, then extend). A closed laptop stops renewing → the lock expires in ≤ 2 min.
3. **Save:** write the new content to Blob as a new object (v43) → `UPDATE docs SET version = 43, blob = … WHERE id = @d AND version = 42 AND lock_token = @token` → add a versions row. A stale editor (lock expired, someone else took it) has an old token → the update matches 0 rows → `423`.
4. **Release:** compare-and-delete (as in Case 6): delete the key only if it's still mine.

### Row B: real-time co-editing
**One owner per document.** All editors of a doc are routed (via the doc router) to the same collab node, which holds the doc in memory and **orders every edit**.

**Every edit:**
1. The client applies the edit locally right away (it feels instant) and sends `{ clientOpId, baseRev, ops }`.
2. The owner **transforms** the op against every op accepted since `baseRev` (**OT**), so it still means what the user intended.
3. It assigns `rev = n + 1` and **appends** it to the op log (`clientOpId` unique: a resent op is accepted once).
4. It **acks** the sender `{ clientOpId, rev }` and **broadcasts** the transformed op to the other editors.

**OT in one example:** the doc is `"cat"`. Alice inserts `"s"` at position 3 (`"cats"`); at the same time Bob inserts `"the "` at position 0. The server takes Bob's first → `"the cat"`. Alice's op was based on the old text, so it's **transformed**: position 3 + 4 (the length of Bob's insert) = 7 → `"the cats"`. Everyone ends with `"the cats"`.

**Open:** read the latest **snapshot** (Blob) + the ops after it from the op log → join the session at that rev.

**Who is connected where (needed to send real-time updates):**
- **Doc router:** `doc → owner collab node` (a lease the owner renews).
- **Connections:** `conns:{doc}` → `{ connId: user, gateway node, cursor }`. The gateway node **registers** a connection on join and removes it on leave; the expiry is refreshed by the socket heartbeat, so a dead gateway's entries vanish on their own.
- **Broadcast:** the owner groups the doc's connections **by gateway node** and sends **one message per node**; each gateway writes it to its local sockets.
- **Failover:** a new owner reads `conns:{doc}` from Redis to know who to send to (and clients reconnect anyway).
- Row C: viewers aren't tracked one by one; every gateway node holding viewers subscribes to the pub/sub channel `doc:{id}`.

**Snapshots and history:** every 500 ops or 1 min, the snapshotter applies the ops to the last snapshot and writes a new one. Version history = a list of snapshot pointers; **restore** writes that old content as a **new** latest version (history is never rewritten).

**Owner node fails:** its lease in the doc router expires → another node takes the doc, rebuilds it from snapshot + op log, and clients reconnect and resend unacked ops (deduped by `clientOpId`).

### Row C: hot document (what changes, in order)
1. **Cap live editors** at 100 (`INCR editors:{doc}`); everyone else joins as a **viewer**.
2. The owner **batches** ops every 50 ms and writes the op log **once per batch** (group commit).
3. It publishes each batch **once** to a pub/sub channel `doc:{id}` instead of sending to 1,000 sockets itself.
4. **Every gateway node that holds viewers** subscribes to that channel (instead of the owner tracking 1,000 connections) and pushes the batch to its own viewers, **throttled** (e.g. every 500 ms); viewers see no live cursors. Explicit connections are kept only for the ≤ 100 editors.
5. **Late joiners** load a **CDN-cached snapshot** (taken every 30 s for hot docs) plus a short tail of ops.

## Which components each path touches
| Requirement | Path | Row A (lock) | Row B (co-edit) | Row C (hot) |
|---|---|---|---|---|
| Start editing | Write | Lock in Redis (NX, lease, fencing token) | Doc router → owner node | + editor cap (≤ 100) |
| Edit | Write | Save: Blob new object → SQL pointer (version + token check) | Owner: transform → rev → op log → ack + broadcast | + 50 ms batches, one op-log write per batch |
| See others' changes | Read | Reload after a save | Pushed by the owner | Pub/sub → every gateway node → viewers (throttled) |
| Presence / who gets updates | Read | "Priya is editing" from the lock | Connections (Redis): user → gateway node + cursor; owner sends 1 message per gateway node | Editors in Connections; viewers via gateway nodes subscribed to `doc:{id}`; no cursors for viewers |
| Open | Read | SQL (ACL, version) → Blob | SQL (ACL) → snapshot → op-log tail | CDN snapshot → short tail |
| Versions | Read / write | Versions (SQL) → Blob | Snapshot pointers; restore = new version | Same as B |
| Sharing | Write | ACL (SQL) | Same | Same |

## Deep dives to drive (learner leads, interviewer pushes)
1. **OT vs. CRDT:** OT needs a central server to order ops (simple clients, small ops; Google Docs). CRDTs give every character a unique id so edits merge in any order (no central order, works offline; Figma, Notion-style, Yjs), at the cost of more metadata. Why offline editing would push you to CRDTs.
2. **Why one owner per doc:** ordering needs one place to decide "who was first". How do you find the owner (doc router lease) and what happens during failover (a few seconds of "reconnecting")?
3. **Lock safety (row A):** the lease expires while Priya's save is in flight. Why a lease alone isn't enough, and how the **fencing token** stops her stale save.
4. **Section locks:** a middle ground between rows A and B: lock per paragraph or section. What's simpler, what breaks (moving text between sections)?
5. **Op log growth:** 5M ops/s; snapshot + compaction; how long to keep raw ops (history granularity vs. cost).
6. **Undo:** undo must undo *my* last edit, not the last edit in the doc: an inverse op, transformed like any other.
7. **Permission change mid-session:** an editor is downgraded to viewer while typing: the owner checks the role on every op (cached ACL) and drops their edits; the gateway sends them a "now read-only" message.

## Interview follow-ups (answer these after the design)
- Alice and Bob both delete the same word at the same moment. Walk through what the server does and what each of them sees.
- The owner node of a document crashes. What happens to edits in flight, and how does everyone get back in sync?
- In row A, Priya's lock expires during a slow save, and Sam takes the lock and saves. Then Priya's save arrives. What stops it?
