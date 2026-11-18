# M16 — HLD fundamentals and real-time delivery

> Plan: `Senior_Engineer_Interview_Plan.html#m16` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Timed designs with estimates and trade-offs, plus a one-page plan for scaling SignalR to 1M connections.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
The vocabulary and method for system design interviews. This module covers:
- the standard building blocks (DNS, load balancers, gateways, CDN)
- how to **size** a system with back-of-envelope maths
- the CAP trade-off
- a **delivery framework** to use every time
- how servers **push** data to clients in real time

### Sessions (in order)
1. **The delivery framework:** requirements → entities → API → high-level design → deep dives → scale → wrap-up; leading the conversation.
2. **The request path:** what happens when you type a URL (DNS, TCP, TLS, HTTP/1.1, 2 and 3).
3. **Building blocks:** L4 and L7 load balancers, reverse proxy, API gateway, CDN, object storage, stateless scaling.
4. **Back-of-envelope maths:** latency numbers, QPS, storage, bandwidth, server count. 🧪 Build a cheat sheet, then size 3 systems.
5. **Data choices as HLD decisions:** SQL vs. NoSQL, replication, partitioning, caching (a recap of Phase 2).
6. **CAP and PACELC,** consistency models in practice, failure domains.
7. **Real-time delivery:** polling, long polling, SSE and WebSockets, and when each fits.
8. **SignalR at scale:** backplane vs. Azure SignalR Service, reconnect storms, presence, resyncing missed messages; mobile push.

### Done when you can answer
- Estimate QPS and storage for a given product in 2 minutes.
- SSE vs. WebSockets: when do you use each?
- What does CAP actually force you to choose, and when?
- How do you scale WebSocket connections across many servers?

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### <Topic name>
- **In plain words:**
- **Problem it solves:**
- **Analogy:**
- **Diagram:** (ASCII/Mermaid or a link to `designs/`)
- **Variations / alternatives:**
- **When not to use it:**
- **Quick-check answers:**

#### Delivery framework (Session 1, 2026-11-18)
- **Steps (45 min):** ① requirements (functional = 3 core features; non-functional = scale, latency, consistency, availability) ~5 → ② entities ~2 → ③ API ~5 → ④ high-level design (make each feature WORK, simple first) ~10–15 → ⑤ deep dives (meet the non-functional ones: scale, bottlenecks, failures) ~10–15 → ⑥ wrap-up (trade-offs, next steps).
- **Leading:** say each step out loud; state assumptions then check; every deep dive traces back to a non-functional requirement; simple first, evolve when a number forces it; maths only when it changes a decision; park deep details ("I'll come back to it").
- **Example:** URL shortener (100:1 reads → cache/CDN; unique codes → counter ranges/Snowflake; shard by code; 302 vs. 301 trade-off).
- **Quick-check answers:** Q1 ⚠️ 6/10 (general; sharpen: functional → HLD boxes, non-functional → which deep dives). Q2 ❌ 3/10 (said requirements; real issue = going deep too early → park it, draw the simple design first). Q3 ✅ 7/10 (clarify requirements; make it concrete by PROPOSING the scope: post, follow, timeline; then ask scale).


#### Request path (Session 2, 2026-11-18)
- DNS (caches → resolver → root → TLD → authoritative; TTL; GeoDNS/Front Door picks nearest region) → TCP (1 RTT) → TLS 1.3 (+1 RTT) → HTTP request → CDN edge or LB/gateway (TLS terminates) → app → cache → DB → response + asset requests.
- ~3–4 RTTs before app work: far users pay that latency → CDN edge, keep-alive/connection reuse, HTTP/3, nearer region.
- HTTP/1.1 one request at a time per connection; HTTP/2 multiplexing over one TCP connection (gRPC); HTTP/3 = QUIC/UDP, 1-RTT setup, no TCP head-of-line blocking, survives network switches.
- **Quick-check answers (revisited after Session 3):** Q1 ✅ 7/10 round trips are far → use the Azure edge (second fix: a region near the users). Q2 ✅ 9/10 HTTP/1.1 one request at a time per connection, HTTP/2 many in parallel on one connection.
- Designs: `designs/request-path-azure.md` (two regions: AKS vs. Container Apps, side by side).


#### Building blocks + edge (Session 3, 2026-11-18)
- Blocks: load balancer (L4/L7, health checks, stateless servers, avoid sticky sessions), reverse proxy, API gateway (auth, rate limits, routing), CDN/edge, object storage (presigned upload URLs). Full walkthrough: `designs/amazon-request-flow.md` (GoDaddy → DNS journeys, edge routing, Azure deployment, product page + checkout journeys).
- **DNS:** registrar (GoDaddy) tells the .com servers who holds your records; authoritative DNS owns the records; resolvers cache for TTL; no global pool. Custom domain = CNAME to the Azure host name (not an HTTP redirect, which exposes the Azure name) + TXT ownership proof + add the domain on the App Service (shared IP, app chosen by Host header) + TLS binding.
- **Edge:** DNS returns the EDGE's IP; the edge picks your config by domain (SNI/Host), routes by path to an origin group, resolves the origin name itself.
- **Why an edge (my words):** without an edge every user gets their own long connection and every reconnect pays the full handshake; with an edge users connect cheaply to a nearby edge and the edge keeps a few long-distance connections to the app open and shared. Plus: cached static content never crosses the ocean, origin offload, WAF/DDoS absorbed, fast failover between regions, private backbone. Handshakes are per CONNECTION (reused across requests), so the edge mostly helps first loads, reconnects and mobile.
- **Session 3 quick-check answers:** 2 GB upload ⚠️ 4/10 (saw it ties up API servers ✅; fix = SAS upload URL → client uploads chunks DIRECTLY to Blob → Event Grid → worker; not a cache). Stale app.js ⚠️ 6/10 (cause ✅; clean fix = versioned file names + long TTL, short TTL on index.html; purge is the coarse fallback). Stateless/sticky sessions: open.
- **CDN:** edge = where (servers near users); CDN = caching content on them; Front Door = CDN + WAF + TLS + global routing. A cache HIT never reaches the app. Edge caches whole HTTP responses (provider's own software, RAM + SSD, key = URL, TTL + LRU), not Redis; Redis is a separate in-region layer your code controls. Code side = only `Cache-Control` headers (`public, max-age=…` for static/public; `private, no-store` for personal; set on blob upload or in the response) + caching ON in the Front Door route. Changes: versioned file names (best) or purge.


#### Deep dive: load balancer (2026-11-18)
- Health checks (`/health` via `AddHealthChecks`) remove failing instances; algorithms: round robin (default), least connections (uneven request cost), weighted (canary), hash (rare). L4 (IP/port, any protocol) vs. L7 (HTTP: path routing, TLS, WAF). Connection draining during deploys.
- Stateless in ASP.NET Core: sessions/cache in Redis, shared Data Protection keys (or cookies break across instances), identity from the token on each request.
- Azure: App Service / Container Apps load-balance built in; AKS → ingress controller; Application Gateway = regional L7 + WAF you ADD (in front of App Service, VMs or AKS); Azure Load Balancer = L4; Front Door = global.
- **Quick-check answers:** Q1 ✅ 8/10 sticky = state lost when server A dies → stateless + separate state store (add: uneven load, painful scaling/deploys). Q2 ✅ 7/10 least connections (add why: round robin keeps feeding an instance stuck on slow requests).


#### Deep dive: API gateway (2026-11-18)
- One front door for all APIs; cross-cutting POLICIES (APIM): validate-jwt, rate-limit-by-key, route by path to services, transforms/headers, versions, caching, developer portal. Not business logic; extra hop, must be highly available.
- Gateway vs. BFF: gateway = shared by all clients, config/policies, usually 1 request → 1 service; BFF = your own code for ONE client, aggregates/shapes responses, holds tokens server-side. Often both: BFFs → APIM → services. Service mesh = service↔service traffic (M18). YARP = build your own in .NET.
- **Quick-check answers:** Q1 ✅ 9/10 JWT validation, routing, logs, rate limiting. Q2 ✅ 9/10 services still check ownership (BOLA): gateway knows WHO, only the service knows who OWNS the object.


#### Deep dive: object storage / Blob (2026-11-18)
- Files by URL in containers (flat names), durable copies (LRS/ZRS/GRS), cheap, scales; the DB stores only the key.
- Upload: API (auth) issues a user-delegation SAS (managed identity, write-only, one blob, 15 min) → client uploads chunks directly (SDK blocks, parallel, retry) → Event Grid BlobCreated → worker. Download: public via Front Door (versioned names), private via ownership check + short-lived READ SAS.
- Tiers: hot / cool / cold / archive (hours to retrieve), lifecycle rules. S3 = same idea (presigned URLs); Azure Files = mountable share; Data Lake = analytics.
- **Quick-check answers:** Q1 ✅ 8/10 bytes are massive, hurt SQL performance → key/URL in SQL (add cost + backups). Q2 ✅ 8/10 short-lived storage URL to the client (add: ownership check first, read-only for that one blob).


#### Back-of-envelope sizing (Session 4, 2026-11-18)
- Cheat sheet: 1 day ≈ 10⁵ s (100M/day ≈ 1,000/s), peak 2–3×; KB/MB/GB/TB/PB = 10³…10¹⁵; latency (memory 100 ns, Redis ~1 ms, SSD 0.1 ms, cross-continent 100–250 ms); capacity per layer in `designs/capacity-per-layer-azure.md`. Size each layer by BOTH throughput and data size; take the bigger. Only compute what changes a decision; end each number with "so…".
- Capacity: API req/s per core ≈ 1000 ÷ CPU ms per request, plan 60–70%. SQL reads ≈ CPU (if hot data in RAM), writes ≈ transaction-log speed; batch writes. Redis is all RAM → memory decides shard count; Redis Cluster = 16,384 hash slots, client routes (one multiplexer), MOVED on changes, hash tags for multi-key.
- **Bandwidth:** all bytes/s in + out (API JSON, uploads, files); files dominate (SnapShare: API 60 MB/s vs. images 30 GB/s). Decides: CDN vs. origin, egress cost (billed per GB), per-server network limits, file sizes / UX, cross-region replication.
- **CDN vs. Redis (my words, refined):** files (any photo, popular or not) → CDN, Blob as origin; the client loads image URLs from the CDN, which handles misses itself; image bytes never in Redis. Redis holds data: per-user feed lists with image URLs, metadata, counters, sessions. Celebrities matter for feed FAN-OUT (M19), not for choosing the CDN. Private files → signed short-lived URLs.
- **LB / gateway instances:** always ≥2 across zones for availability (capacity is rarely the issue for an LB; APIM needs units for policy work). "Who balances the LBs?" ends in the network: one virtual IP spread by routers (ECMP) across instances; Front Door = anycast from ~190 cities; Azure Load Balancer is part of the network fabric; self-hosted = active–passive pair with a floating IP.
- **Vertical vs. horizontal (my words, refined):** APIs scale OUT by default; UP only when each single request is CPU/RAM-heavy (or fix a leak). DBs scale UP first (hot data must fit in RAM; one write primary), reads also scale OUT via replicas/cache, writes/size beyond the biggest box → shard.
- **SnapShare worksheet (learner did it level by level):** `designs/snapshare-sizing.md`. Gaps: day→second slip (10,000 vs. 1,000/s for 100M/day), missed images as the biggest load, Redis node ~100K ops/s (not 1K), 1 PB not 2 PB. Strong: method, storage maths, SQL load, viral-post fix (Redis INCR + batch flush).


#### Data choices as HLD decisions (Session 5, 2026-11-18)
- Per data type ask: how read (ID / joins / text / time range), write volume, transactions?, size → pick: SQL (ACID, joins), Cosmos (by key, scale out, global), wide-column (massive appends), Redis (hot, counters, sessions), AI Search (text), Blob (files), Event Hubs (streams), warehouse (analytics). Then replication / partitioning / caching. One reason per choice; add stores only when a number forces it.
- SnapShare: users + follows → SQL; posts → Cosmos /userId; likes → Cosmos /postId + Redis counters; feeds → Redis; photos → Blob + Front Door; search → AI Search via events; activity → Event Hubs.
- **Cosmos partition key:** hash(key) → which server holds the item; queries with the key hit one partition (point read ~1 RU), without it fan out to all; pick the most-filtered field that spreads evenly, < ~20 GB per key value; hot key (viral post) → Redis counter.
- **Quick-check answers:** Q1 ✅ 8/10 orders/payments → SQL (add: transactions, consistency). Q2 ⚠️ 6/10 AI Search ✅ but typos = FUZZY full-text; nearest neighbours = vector/semantic search; SQL LIKE '%x%' scans, no typos, no ranking. Q3 ✅ 9/10 one SQL, measure, scale API out, then cache/replicas for reads.


#### CAP / PACELC / failure domains (Session 6, 2026-11-18)
- CAP: during a network partition each copy refuses (C) or answers possibly stale (A); P is unavoidable, so no "CA". PACELC: normally, latency vs. consistency (= sync vs. async replication).
- Per feature, not per system: payments/stock/balance → C (error beats double spend); likes/feeds/views → A; own profile → Session (read-your-writes). Azure SQL single primary = C; Cassandra-style = A/L; Cosmos = 5 levels (Strong, Bounded staleness, Session default, Consistent prefix, Eventual).
- **My words (refined):** strong = sync to replicas; eventual = async. Precision: sync replication = no data loss; strong READS also need the primary or a quorum (a sync SQL replica can apply the change a few ms later).
- Failure domains: rack → zone → region; never put all copies in one domain (zone-redundant; multi-region for critical).
- Quick check skipped (open).


#### Real-time delivery (Session 7, 2026-11-18)
- Polling (simple, wasteful, delayed) · long polling (held request, one per event) · SSE (server → client stream over HTTP, auto-reconnect: feeds, notifications, tracking, AI token streaming) · WebSockets (two-way, lowest overhead: chat, games, collaboration). Rare updates → poll. Closed app → mobile push (Notification Hubs).
- Scaling: long-lived connections are stateful (each on one server) → cross-server delivery needs a shared connection registry (Redis, TTL/heartbeat) or a pub/sub backplane; Azure SignalR Service manages it.
- **Quick-check answers:** Q1 ✅ 9/10 driver position → SSE (one-way). Q2 ✅ 7/10 chat → WebSockets (better reason: both sides send often in real time). Q3 ⚠️ 6/10 user→server dictionary: must be SHARED (Redis), handle stale entries; simpler common answer = pub/sub backplane.


#### SignalR at scale (Session 8, 2026-11-18)
- SignalR = doorbell, not mailbox. Scale-out: Redis backplane (servers hold connections, every server gets every message, sticky handshake) vs. Azure SignalR Service (service holds connections, app servers stateless, units of ~1,000 connections, same hub code via AddAzureSignalR).
- Reconnect storms → client backoff + jitter (withAutomaticReconnect), gradual deploys, headroom. Presence → Redis set of connection ids per user + heartbeat/TTL (multi-device). Missed messages → save to DB with sequence numbers first, then push; reconnect → SyncSince(lastSeen); app closed → Notification Hubs.
- **Quick-check answers:** Q1 ✅ 8/10 the service holds the connections. Q2 ⚠️ 5/10 said per-user queue FIFO; standard = DB + sequence numbers, client asks "since #40" (per-device last-seen). Q3 ✅ 7/10 connect gradually (name it: backoff + jitter).


#### M16 done-when (2026-11-18)
- QPS/storage in 2 min ✅ (SnapShare; watch day→second). SSE vs. WebSockets ✅. Scaling WebSockets ✅ (backplane / Azure SignalR Service).
- CAP ✅ 8/10: "choose consistency (all reads see the same data) or availability (data available but maybe stale); withdraw money → C, likes → A." Missing: only DURING a partition (else PACELC latency vs. consistency); C means the user sees an error / try again.


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
