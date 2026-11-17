# Progress

> Synced through git. Pull before you start, and commit and push when you stop.
> Ask Claude: "continue from PROGRESS.md" to start, and "wrap up" to update this file.

## Now
- **Current module:** M6 — Database internals, transactions & sharding (Phase 2 started)
- **Next step:** Restart M6 Session 1 (Indexes) from the beginning, in small pieces with a short check after each: (1) what an index is + seek vs. scan, (2) clustered vs. non-clustered, (3) key lookups + covering indexes, (4) composite indexes + column order, (5) when indexes hurt / how seeks get lost. Then Session 2. Code samples instead of Docker. CP1 deferred.
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
| 2 | M6 | Database internals & sharding | 🟨 |
| 2 | M7 | NoSQL, storage engines, search | ⬜ |
| 2 | M8 | Redis & distributed caching | ⬜ |
| 2 | M9 | Messaging & queue reliability | ⬜ |
| 2 | M10 | Kafka, event sourcing, CQRS | ⬜ |
| 2 | M11 | Outbox, inbox, retries, DLQ | ⬜ |
| 2 | M12 | Multiple DBs, 2PC, Saga | ⬜ |
| 2 | M13 | Concurrency & resilience | ⬜ |
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
- Priority: Phase 2 (M6–M15) and Phase 3 (M16–M21) need deep understanding: run the labs, answer at least one quick check per topic, end each module with interview follow-ups.
- M2 revisit (mock round 2): Q3 generic repository verdict, Q5 "must never be skipped" → event + Outbox, Q10 DB transaction vs. broker publish. Retry before CP1.
