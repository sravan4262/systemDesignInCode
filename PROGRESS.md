# Progress

> Synced through git. Pull before you start, and commit and push when you stop.
> Ask Claude: "continue from PROGRESS.md" to start, and "wrap up" to update this file.

## Now
- **Current module:** M14 Security & API design
- **Next step:** M14 Session 1 (OAuth2 + OIDC + PKCE). Agreed cut (2026-11-17):
  - **M14 (7):** 1 OAuth2/OIDC/PKCE + client credentials · 2 JWT validation + revocation · 3 BFF + cookies · 4 Azure identity + secrets (Entra, managed identity, DefaultAzureCredential, Key Vault; mTLS one-liner) · 5 Authorization + BOLA (code) · 6 API design (REST/gRPC/GraphQL, ProblemDetails) · 7 API mechanics (cursor pagination, versioning, idempotency middleware). One-liners: SAML, OWASP API Top 10 list.
  - **M15 (6):** 1 three pillars · 2 OpenTelemetry tracing across services + queue · 3 structured logs + correlation IDs · 4 key metrics (lag, oldest age, DLQ, outbox backlog, pool, deadlocks) · 5 SLIs/SLOs/alerting · 6 testing strategy. Incident drill → CP2 mock.
  - M13 done-when: Q1 (deadlock fix) and Q2 (Redis lock + fencing) open for CP2.
- **Study mode:** Code samples instead of Docker. Mechanism topics: core mechanism + one worked example first. CP1 deferred.
- **Machine last used:** Mac

## Status
Legend: ⬜ not started · 🟨 in progress · ✅ done (exit deliverable met)

| Phase | Item | Title | Status |
|---|---|---|---|
| 1 | M1 | OOP, SOLID, clean design | ✅ |
| 1 | M2 | Design patterns | ✅ (moved on; see open questions) |
| 1 | M3 | LLD interview problems | ✅ (moved on; design-only, no tests/walkthroughs) |
| 1 | M4 | Concurrent LLD & machine coding | ✅ (concepts + core code; no tests by choice) |
| 1 | M5 | .NET platform internals | 🟨 (6 hot topics covered; rest deferred) |
| 1 | CP1 | Checkpoint: LLD & .NET | ⬜ (deferred) |
| 2 | M6 | Database internals & sharding | ✅ (code samples, no Docker; see open questions) |
| 2 | M7 | NoSQL, storage engines, search | ✅ (overview by choice) |
| 2 | M8 | Redis & distributed caching | ✅ (overview by choice; see open questions) |
| 2 | M9 | Messaging & queue reliability | ✅ (condensed track; see open questions) |
| 2 | M10 | Kafka, event sourcing, CQRS | ✅ (Kafka core; ES/CQRS skipped by choice) |
| 2 | M11 | Outbox, inbox, retries, DLQ | ✅ (outbox + inbox code, failure catalog) |
| 2 | M12 | Multiple DBs, 2PC, Saga | ✅ (Saga concepts; see open questions) |
| 2 | M13 | Concurrency & resilience | ✅ (Sessions 1–6; load shedding skipped; see open questions) |
| 2 | M14 | Security & API design | ⬜ |
| 2 | M15 | Observability, testing, ops | ⬜ |
| 2 | CP2 | Checkpoint: distributed systems | ⬜ |
| 3 | M16 | HLD fundamentals & real-time | ⬜ |
| 3 | M17 | Distributed systems theory | ⬜ |
| 3 | M18 | Architecture & domain design | ⬜ |
| 3 | M19 | HLD patterns & case studies I | ⬜ |
| 3 | M20 | HLD case studies II | ⬜ |
| 3 | M21 | Advanced architecture & cloud ops | ⬜ |
| 3 | CP3 | Checkpoint: HLD | ⬜ |
| 4 | M22 | LLM apps & RAG | ⬜ |
| 4 | M23 | LLM internals, inference, adaptation | ⬜ |
| 4 | M24 | Agents & evals | ⬜ |
| 4 | M25 | Production AI & AI system design | ⬜ |
| 4 | CP4 | Checkpoint: AI design | ⬜ |
| 5 | M26 | AI coding, code review, comprehension | ⬜ |
| 5 | M27 | Agentic tickets & live debugging | ⬜ |
| 5 | M28 | Capstone integration | ⬜ |
| 5 | CP5 | Checkpoint: AI coding | ⬜ |
| 6 | M29 | Story bank & question bank | ⬜ |
| 6 | M30 | Deep dives, motivation, leveling | ⬜ |
| 6 | CP6 | Checkpoint: behavioral | ⬜ |
| 7 | M31 | Resume & positioning | ⬜ |
| 7 | CP7 | Checkpoint: recruiter screen | ⬜ |
| 8 | M32 | Final mock interviews | ⬜ |

## Failure checklist (plan section 4)
Ticked items (by ID, e.g., c1, c13): —

## Session log
<!-- Newest first. Format: YYYY-MM-DD · machine · module · what was done · what's next -->
- 2026-11-17 · Mac · M13 · S1 contention at scale (pessimistic vs. atomic conditional UPDATE vs. optimistic; buckets `BucketedInventory.cs`), S2 deadlocks (blocking vs. cycle, lock order, retry 1205), S3 blocking + lock escalation + pool exhaustion (batching), S4 distributed locks (Redis NX/PX/token release, fencing, sp_getapplock, claim batches for big jobs), S5 timeouts + retries (amplification 64, deadlines, CancellationToken), S6 circuit breaker MVP + bulkhead + fallback (`Resilience.cs`, Polly v8); learner's breaker explanation ~8.5/10. S7 load shedding skipped. Next: M14.
- 2026-11-17 · Mac · M9–M12 (messaging track) · S1 refresher (queue vs. pub/sub, fanout/topic/direct exchanges, shared queue for competing consumers, acks, guarantees, ordering, DLQ, idempotency). S2 Kafka (partitions, offsets per group per partition, consumer groups, parallelism cap, acks/ISR, commits, lag, exactly-once scope). S3 RabbitMQ vs. Kafka (queue vs. log, use-case table). S4 outbox (`M11.Messaging/Outbox.cs`, relay with UPDLOCK+READPAST). S5 inbox (`Inbox.cs`, PK as referee, Stripe idempotency key); outbox/inbox Q&A ~6.5/10. S6 failure catalog. S7 Saga (choreography vs. orchestration, Saga vs. 2PC, compensation, pivot). Track closed. Next: M13.
- 2026-11-17 · Windows · M7 + M8 · M7 all 8 sessions at overview level (storage engines, document modelling, Cosmos DB, Cassandra, SQL vs. NoSQL order history, search engines, search sync, choosing a store). M8 all 8 sessions at overview level (why distributed, caching patterns, TTL/eviction, data structures, invalidation, stampede/avalanche/penetration/hot keys, Redis rate limiting incl. gateway vs. app, Redis down). M8 Q&A ~2.9/6. Planned a condensed messaging track for M9–M12 (see Now). Next: messaging track Session 1.
- 2026-11-17 · Windows · M6 · Session 8 sharding (shard key, directory `ShardRouting.cs`, range, hash + fixed buckets `HashShardRouting.cs` measured 79.8% vs 19.8% keys moved, hot keys, secondary index by another key, replicas per shard, multi-region home region, API-level resolution `ShardedApi.cs`, guide `designs/sharding-request-flow.md`). Session 9: global IDs (Snowflake), cross-shard pagination (keyset + merge, search index), tenant move `TenantMove.cs` (copy → catch up → freeze/fence → switch → cleanup). Q&A 3 rounds ~7.3/17; gaps logged in the M6 README. M6 closed. Next: M7.
- 2026-11-17 · Mac · M6 · Session 1 indexes (seek/scan, clustered vs. non-clustered, key lookup + INCLUDE, composite order, SARGable) and Session 2 (execution plans, parameter sniffing + RECOMPILE trade-off, connection pooling/exhaustion, Little's Law, ladder before sharding). Gaps: filter order vs. index column order (corrected), low CPU + pool exhaustion = idle/blocked holders. Session 3: ACID, read anomalies, lost update, write skew (corrected: lost update ends at 9 not -1; name is 'lost update'). Session 4: lock-based levels (S/X lock duration, range locks) and RCSI vs. Snapshot (RCSI doesn't stop lost updates; neither stops write skew). Session 5: optimistic (rowversion, retry vs. 409, bad under contention = wasted retries) and pessimistic (UPDLOCK, READPAST). Session 6: replication (primary-replica, sync/async, read-your-writes, listener failover, multi-primary, read replica routing) + `ReadWriteRouting.cs`; mock ~5/10. Next: Session 7–8.
- 2026-11-16 · Mac · Phase 2/3 setup + M6 · Wrote Learning plans (big picture → sessions → done-when) for M6–M21; learner prioritizes Phases 2–3; code samples instead of Docker labs. Replication vs. sharding clarified. M6 Session 1 (indexes) taught, quick check pending. Next: answer it, then Session 2.
- 2026-11-16 · Mac · M4 · LRU cache, eviction policies, token bucket + sliding window limiters (IRateLimiter), Channels/backpressure (concept), KV store with nested transactions, lock / Mutex / ReaderWriterLockSlim examples, thread-safe logger (concept). M5 hot topics (concept only): async/await + sync-over-async, DI lifetimes + captive dependency, middleware order, IHttpClientFactory, N+1, DbContext lifetime. Next: CP1 or M6.
- 2026-11-16 · Mac · M3 · Parking Lot break-it (partial): last-slot race (lock → conditional UPDATE / UPDLOCK+READPAST + unique index), double exit (atomic status claim, idempotency key, ownership-aware release), gateway timeout = unknown outcome (PaymentPending + reconciliation). Learner chose to skip remaining drills and focus on LLD design. Movie Ticket Booking designed (requirements → entities with ShowSeat → services), no code by choice. Expense Sharing designed (Splits list, split Strategy, pair balances, settlements). ATM: clarifications + entities + state machine; services skipped. Hotel Booking designed (RoomInventory per night, all-nights atomic claim). Notification Service clarifications (commands vs events, priorities, preferences at send time, idempotency key). Closed M3 by choice. Next: M4.
- 2026-11-16 · Windows · M3 · Parking Lot interview-style: clarifying questions → requirements → entities → service signatures → Enter/Exit flows → concurrency (lock vs. atomic UPDATE) → data structures (min-heap per slot size, ConcurrentDictionary for tickets) → simple in-memory code. Key gap: method signatures (inputs = what the caller knows). Next: break it + verbal walkthrough, then Movie Ticket Booking.
- 2026-11-16 · Windows · M2 → M3 · Added Repository/Unit of Work critique (`RepositoryUnitOfWork.cs`; EF Core InMemory via central package versions). Mock round 2: ~4.6/10 (harder questions; see README). Closed M2 by choice and moved to M3. Recorded the LLD format preference in CLAUDE.md (problem statement first, then back-and-forth). Next: M3 Parking Lot.
- 2026-11-16 · Windows · M2 · Restarted M2 and covered 8 patterns (7-step each): Strategy, Factory, Builder, Singleton, Adapter (+Facade), Observer, Command, Chain of Responsibility. Dropped State and Decorator from the plan by choice. Before/After files with a nested `Program` (ASP.NET Core wiring) in `M02.Checkout/DesignPatterns/`; project now references `Microsoft.AspNetCore.App`. Concepts entries written; ~45 review questions parked in the README. Mock round 1: ~6.6/10 (see README). Added a "Concept code" convention to CLAUDE.md (2–3 members per class). Next: Q&A round 2, then M2 wrap-up tasks.
- 2026-11-16 · Mac · M2 · First pass at Strategy and Factory (`Strategy.cs`, `Factory.cs`); Factory quick check not answered. Next: restart design patterns from the beginning.
- 2026-11-15 · Mac · M1 · Covered encapsulation, abstraction/polymorphism, and SOLID (7-step each); violation-vs-fix code in `M01.Payments/SolidPrinciples/`; Concepts entries written. Payment-engine build skipped by choice; test projects removed (create on request). Next: M2.
- 2026-11-15 · Mac · setup · Plan finalized; repo scaffolded (CLAUDE.md, PROGRESS.md, module folders). Next: start M1.

## Open questions
- M13 done-when: breaker goes max failures → OPEN (not half-open), then half-open = one trial call. Q1 deadlock-without-more-locks and Q2 Redis lock + fencing unanswered. Retry before CP2.
- M13 distributed locks quick check unanswered: why expiry + the slow-holder problem it creates; token-checked release; fencing token checked by the RESOURCE (DB), not the lock service.
- M13 deadlocks: Q2 fix = consistent lock order (both touch Orders → Inventory); Q3 = retry the WHOLE tx, safe only if no external side effects inside it. Blocking vs. deadlock (line vs. cycle) and optimistic (no locks) vs. pessimistic (lock at read until commit) were mixed up; retry before CP2.
- Messaging track gaps (retry before CP2): Kafka assigns PARTITIONS to instances (not round-robin messages; extra instances idle). Kafka part 2 unanswered: acks=1 loss + fix, auto-commit loss, 7th instance with 6 partitions. Inbox: unique constraint + one tx, not check-then-act / moving `Add` first; failed commit = retry not DLQ. Outbox: READ UNCOMMITTED is wrong for relays. Session 6 failure scenarios (poison in Kafka, retry storm, ordering broken by retries) unanswered. Saga: why not 2PC, double refund prevention unanswered. Outbox follow-ups: relay holding locks during slow publish, outbox vs. Redis, unsent-age alert.
- M8 caching Q&A (~2.9/6): name failure modes (stampede → single-flight + early refresh); remaining delete race (slow reader) + TTL cap; Redis down must also protect the DB and is a circuit breaker; balances aren't cached because they must be current; cache-aside vs. write-through (skipped). Retry before CP2.
- M6 sharding Q&A (~7.3/17): replicas never take writes; timeout = unknown outcome → idempotency key; monotonic reads → sticky replica; cross-DB transfer → Saga; hot key ≠ sharding problem; fence rejects (no forwarding); key not in query = slow fan-out; fixed buckets avoid rehash. Full list in the M6 README. Retry before CP2.
- M6 replication (mock ~5/10): stale reads are fixed by routing (read-your-writes), not by making replicas sync; one primary + sync replica in another zone + async replica in another region; during failover writes fail briefly → retry + idempotent writes. Retry before CP2.
- Priority: Phase 2 (M6–M15) and Phase 3 (M16–M21) need deep understanding: run the labs, answer at least one quick check per topic, end each module with interview follow-ups.
- M2 revisit (mock round 2): Q3 generic repository verdict, Q5 "must never be skipped" → event + Outbox, Q10 DB transaction vs. broker publish. Retry before CP1.
