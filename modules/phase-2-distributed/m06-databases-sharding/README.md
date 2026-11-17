# M6 — Database internals, transactions, and sharding

> Plan: `Senior_Engineer_Interview_Plan.html#m6` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A C# routing layer that sends tenants to the correct shard, plus measurements and a rebalancing exercise.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
One database done right, then split when one isn't enough. First learn how SQL Server finds rows fast (indexes) and keeps concurrent transactions from corrupting each other (isolation levels, locks). Then **copy** the database (replication) for read scale and availability, and **split** it (sharding) for write scale and size. Sharding is the last resort, and knowing what to try first is a senior signal.

### Sessions (in order)
1. **Indexes:** B-tree, clustered vs. non-clustered, seek vs. scan, composite-index column order, covering indexes (`INCLUDE`). 🧪 Compare execution plans before and after adding an index.
2. **Query tuning and connection pooling:** reading plans, key lookups, parameter sniffing, pool exhaustion.
3. **Transactions and anomalies:** dirty read, non-repeatable read, phantom, lost update, write skew. 🧪 Two SQL sessions side by side, reproducing each one.
4. **Isolation levels:** Read Uncommitted, Read Committed, RCSI, Repeatable Read, Snapshot, Serializable. Which anomaly each one prevents, and what it costs.
5. **Optimistic vs. pessimistic concurrency:** `rowversion` with an EF Core concurrency token; `UPDLOCK`, `HOLDLOCK` and `READPAST`. 🧪 Fix a lost update both ways.
6. **Replication:** primary-replica (master-slave) with a read/write split; single-primary vs. multi-primary vs. leaderless (overview, with depth in M17/M21); sync, async and semi-sync; replica lag; read-your-writes; failover and promotion; replica vs. backup vs. snapshot. Replication = copies of all the data; sharding = splitting the data. They're often combined.
7. **Before sharding:** scale up, read replicas, caching, archiving, partitioning. When sharding is premature.
8. **Sharding strategies:** range, hash and directory; choosing the shard key; hot partitions. 🧪 3 SQL containers and a C# shard resolver.
9. **Living with shards:** cross-shard queries, pagination, global IDs, rebalancing and migration. 🧪 Move a tenant from one shard to another.

### Done when you can answer
- Why does an index on `(CustomerId, Date)` help `WHERE CustomerId = …` but not `WHERE Date = …`?
- Which isolation level prevents lost updates in SQL Server, and what does RCSI change?
- Optimistic vs. pessimistic concurrency: when do you use each?
- How do you pick a shard key for a multi-tenant SaaS, and what is a hot partition?
- What do you try before sharding?

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### Indexes (Session 1)
- **In plain words:** A sorted copy of column values plus a pointer to the row, stored as a B-tree. Because it's sorted, SQL walks down the levels to the matching values (seek, ~3–4 page reads) instead of reading every row (scan).
- **Seek vs. scan:** seek cost grows with the number of matches; scan cost grows with table size. A scan is right when the query needs most rows (e.g. `SUM` over the table, a status that 80% of rows have).
- **Clustered vs. non-clustered:** clustered = the table itself stored in key order, so only one. Non-clustered = a separate copy of the key columns + the clustered key as pointer; the other columns are not stored.
- **Key lookup / covering:** columns not in the index → a trip back to the table per row (key lookup); too many matches → SQL scans instead. `INCLUDE` columns are carried at the leaf so the trip disappears. Key = what you filter on (sorted); INCLUDE = what you select (just present).
- **Composite order:** sorted by the first column, then the next within it. Seeks only if the query uses the leading column (`CustomerId`, or `CustomerId` + `OrderDate`); `OrderDate` alone scans. Filter order in `WHERE` never matters; equality columns first, then one range/sort column.
- **When indexes hurt:** every index is updated on every write → few indexes on write-heavy tables (outbox: one small filtered index for the relay). Non-SARGable filters lose the seek: function or maths on the column (`YEAR(OrderDate)` → use a range), implicit conversion, leading `%`, OR across columns. Low-selectivity column → filtered index.
- **Analogy:** the index at the back of a book; a phone book sorted by (surname, first name).

#### Query tuning & connection pooling (Session 2)
- **Execution plans:** SQL's recipe for a query. Look for: scans on big tables for few rows, costly Key Lookups (fix: `INCLUDE` the columns or select fewer), estimated ≠ actual rows (stale statistics / sniffing), sort or hash spills. Use the ACTUAL plan, `SET STATISTICS IO ON` (logical reads), Query Store (top queries by total time, plan changes).
- **Parameter sniffing:** the plan is built from the first parameter value and reused for every call; results stay correct, but a skewed value gets the wrong strategy (e.g. seek + 1M key lookups ≈ 3M random page reads vs. a 62,500-page sequential scan). Matters most with a non-covering index (two possible plans). Fixes: covering index first; `OPTION (RECOMPILE)` for heavy, infrequent queries (compile CPU every call); `OPTIMIZE FOR`; Query Store plan forcing; PSP in SQL 2022.
- **Connection pooling:** Open borrows, Dispose returns (default max 100 per connection string). Exhaustion = connections held too long. Low DB CPU + exhaustion → held while idle/blocked (leak, held across HTTP calls, lock blocking, sync-over-async) → check session DMVs. Raising Max Pool Size only delays it and pushes load onto SQL.
- **Connections in use = requests/sec × seconds held** (Little's Law). Before sharding: tune queries → fewer calls (cache, batch, fix N+1) → throttle → scale up → read replicas → queues/CQRS → split by service → shard (last, when one primary's writes/size are exceeded).

#### Transactions & anomalies (Session 3)
- **Transaction / ACID:** a group of changes that succeed or fail together. Atomicity (all or nothing), Consistency (constraints hold), Isolation (others don't see half-done work; the adjustable one), Durability (written to the transaction log before COMMIT returns; replayed after a crash). Every SQL statement is its own transaction; several statements are atomic only inside one explicit transaction (EF: one `SaveChanges`). Never covers a second DB or a broker publish.
- **Inside a transaction nothing is committed until COMMIT:** at Read Committed others wait (locking) or see the previous committed version (RCSI), so a rollback leaves nothing behind. The DB doesn't replay failed transactions; the app retries (so transactions must be safe to retry). WAL replay after a crash only restores committed work.
- **Dirty read:** reading uncommitted data that may roll back (Read Uncommitted / `NOLOCK`). Example: NOLOCK fulfilment job ships an order whose checkout rolled back. NOLOCK can also double-count/skip rows. Fix blocking with RCSI, not NOLOCK.
- **Non-repeatable read:** same row, same transaction, different value (allowed at Read Committed, the default).
- **Phantom:** same query returns new/missing rows (count/limit checks → overbooking).
- **Lost update:** read → compute in app → write back; one write overwrites the other (stock 10 → two sales → 9). Default EF pattern. Fixes: atomic `SET Stock = Stock - 1 WHERE Stock >= 1`, `UPDLOCK`, `rowversion`.
- **Write skew:** each checks a rule, each updates a DIFFERENT row, together they break it (on-call doctors, overlapping bookings). Even Snapshot allows it; rowversion on your own row can't see it. Fixes: Serializable / `UPDLOCK, HOLDLOCK` on the rows the rule reads, a constraint/unique index, or materialise the conflict into one shared row (like Hotel `RoomInventory`).
- **Default:** Read Committed (Azure SQL: with RCSI). Keep it, and protect check-then-act operations specifically.

#### Isolation levels (Session 4)
- **Mechanism (locking levels):** writers always take X locks held to COMMIT; levels differ in readers' S locks. Read Uncommitted: none (dirty reads). Read Committed: S lock released after each row (non-repeatable reads, phantoms, lost updates allowed). Repeatable Read: S locks held to COMMIT (stops non-repeatable reads; lost update becomes a deadlock; phantoms still get in because new rows weren't locked). Serializable: + key-range locks (stops phantoms and write skew; most blocking and deadlocks; needs an index or it locks far more).
- **Hints:** `NOLOCK` = Read Uncommitted, `HOLDLOCK` = Serializable for one table, `UPDLOCK` = read with intent to write.
- **Versioning:** old committed row versions kept in tempdb; readers don't block writers and vice versa. RCSI = snapshot per statement (DB-wide replacement for Read Committed; default on Azure SQL); the safe alternative to NOLOCK. Does NOT prevent lost updates. Snapshot = snapshot per transaction (repeatable reads, no phantoms; same-row write conflict → error 3960, retry). Neither prevents write skew. Cost: tempdb growth, 14 bytes/row, slightly stale reads.
- **Choosing:** RC + RCSI as default; Snapshot for consistent multi-query reports; atomic update / UPDLOCK / rowversion for read-then-update; Serializable/HOLDLOCK or a constraint or a shared counter row for check-range-then-insert and write skew; never NOLOCK.

#### Optimistic vs. pessimistic concurrency (Session 5)
- **Optimistic:** no lock on read; remember the version; `UPDATE ... WHERE RowVersion = @read` → 0 rows = conflict (EF: `[Timestamp]` → `DbUpdateConcurrencyException`). Then reload and retry (mechanical change, capped ~3) or return 409/412 to the user (a human decision). Never ignore it. `rowversion` = SQL Server's automatic DB-wide change counter (not a time, not a GUID; you never set it). Over HTTP: ETag + If-Match → 412. Bad under high contention: most attempts conflict and retry (wasted work), not because of locks.
- **Pessimistic:** `SELECT ... WITH (UPDLOCK, ROWLOCK)` inside a transaction; other would-be writers wait at the read; plain readers aren't blocked. U lock conflicts with U, so no S→X upgrade deadlock (unlike Repeatable Read). Keep the work short; lock in a consistent order. `UPDLOCK + READPAST` = claim the next free item (job queues, outbox relay, nearest slot). Works across app servers because the lock lives in the DB.
- **Choose:** one SQL statement → atomic conditional UPDATE; complex logic + rare conflicts / across HTTP → optimistic; complex logic + frequent conflicts → pessimistic; many workers taking items → UPDLOCK + READPAST.

#### Replication (Session 6, part 1)
- **Primary-replica:** one primary takes all writes and streams its transaction log to replicas, which replay it (SQL Server Always On AG / Azure SQL). Built into the engine (not app code, not jobs; log shipping is the old job-based exception). Helps reads + availability, never writes.
- **Sync vs. async = data loss on failover:** sync → commit waits for the replica to harden the log (few ms if nearby, no data loss); async → commit returns immediately (fast, can lose the last seconds). Sync close (other zones), async far (other regions). Sync replica unreachable → after ~10 s the primary continues without it (effectively async; failover would lose data; log grows until it catches up).
- **Stale reads are a routing problem, not a sync problem:** even a sync replica redoes slightly later. Read-your-writes fixes: after a write, route that user's reads to the primary for a few seconds (`ReadWriteRouting.cs`), return data in the write response, route must-be-fresh reads to the primary. Different replica lag → monotonic-read violations → sticky replica per session.
- **Routing in code:** the caller states intent (Write/Read); writes → primary listener; reads → replica (round-robin) unless the user just wrote. Commonly: one listener + `ApplicationIntent=ReadOnly` and let read-only routing pick.
- **Failover:** the cluster detects failure and promotes a sync replica; the listener / failover-group endpoint is re-pointed; writes fail for 10–30 s → app retries (`EnableRetryOnFailure`) and writes must be idempotent. RPO = data you can lose; RTO = time down.
- **Multi-primary:** Cosmos DB, Cassandra, some MySQL/Postgres setups (not SQL Server AGs); write conflicts (last-writer-wins, merge, CRDTs) → use one primary unless low-latency writes worldwide are needed.
- **Code:** `src/M06.Databases/ReadWriteRouting.cs`

#### Replication mock (2026-11-17): ~5 / 10
- Strong: Q1 replication vs. sharding, Q2 sync vs. async mechanism, Q8 "not sharding for a read-heavy DB".
- Revisit: Q4/Q5 "make it sync" ≠ fix for stale reads (use read-your-writes routing / return data from the write); Q6 one primary + sync replica in another zone + async replica in another region, region failover loses seconds; Q7 listener re-pointing, writes fail briefly → retry + idempotency.

#### Sharding strategies (Session 8)
- **In plain words:** split one big DB into shards; a shard key + strategy decides which shard holds each row. Replication = copies (reads, failover); sharding = split (writes, size). Big systems do both: replicas per shard.
- **Shard key:** in almost every query (else scatter-gather: correct but slow, fails if any shard is down), spreads data AND load evenly, never changes. SaaS → `TenantId`; messaging → `ChatId`; a second common key → a secondary index/copy sharded by that key (async, targeted fan-out or denormalized).
- **Directory** (`ShardRouting.cs`): `tenant → shard` table in a replicated catalog DB, cached with a TTL; onboarding = place by load (status Provisioning → Active); adding a shard moves no one; per-tenant control (big tenant → dedicated shard). Shard list in config/Key Vault (managed identity), tenant map in a table.
- **Range:** simple, range scans; growing keys → all new rows on the last shard (hot).
- **Hash** (`HashShardRouting.cs`): stable hash (`XxHash32`, never `GetHashCode()`), even spread, no lookup. `hash % N` moved 79.8% of keys going 4 → 5 shards; **fixed buckets** (1,024, never changes, ≫ shard count) + small bucket → shard map moved 19.8%. Hybrid: hash's even spread + directory's control.
- **Hot keys:** sharding can't split one key. Reads → cache/CDN, replicas, fan-out on write. Writes → batch/aggregate (Redis INCR), split the key (salting).
- **API flow** (`ShardedApi.cs`, `designs/sharding-request-flow.md`): tenant from token → shard (cached directory) → primary/replica → pooled connection; multi-region adds home region first (global directory, token claim, forward/redirect).

#### Living with shards (Session 9)
- **Global IDs:** per-shard `IDENTITY` collides. Options: random GUID (index fragmentation), v7 GUID, Snowflake = time + node id (unique per generator) + counter (unique within the ms); never embed a physical shard (use bucket/tenant). Risks: duplicate node ids, clock going backwards.
- **Pagination:** `OFFSET` cost grows with page × shards; keyset/cursor `(CreatedAt, Id)` with per-shard TOP N + merge; Id = tiebreaker. Prefer single-shard (per tenant); cross-shard lists → search index with `search_after` (eventually consistent); analytics → warehouse.
- **Rebalancing** (`TenantMove.cs`): copy → catch up → freeze (status Moving via version CAS + **fence** the old shard) → final catch-up + verify → switch directory → clean up later. Stale caches: the fenced old shard rejects writes → `ShardAwareWriter` invalidates + re-resolves → new shard (during freeze → 503 Retry-After). Caveat: fence vs. in-flight writes needs a lock on the fence row.

#### Sharding & replication Q&A (2026-11-17): ~7.3 / 17 over three rounds
- Strong: before-sharding answer (don't shard at 40% CPU), bucket reasoning (round 2), rebalancing phases.
- Revisit (recurring): (1) replicas never take writes; (2) timeout = unknown outcome → idempotency key + unique constraint ("effectively once", not exactly once); (3) different replica lag → monotonic reads → sticky replica per session, not a cache; (4) cross-database transfer → Saga (not a "transaction in app code" = 2PC); (5) hot key isn't a sharding problem; (6) the old shard rejects (fence), it doesn't forward; (7) shard key not in the query = slow fan-out, not wrong data; (8) fixed buckets avoid rehashing (plain `% N` doesn't).

#### <Topic name>
- **In plain words:**
- **Problem it solves:**
- **Analogy:**
- **Diagram:** (ASCII/Mermaid or a link to `designs/`)
- **Variations / alternatives:**
- **When not to use it:**
- **Quick-check answers:**

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
