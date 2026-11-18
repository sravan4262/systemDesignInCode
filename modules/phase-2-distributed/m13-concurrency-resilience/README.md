# M13 — Concurrency, coordination, and resilience

> Plan: `Senior_Engineer_Interview_Plan.html#m13` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A concurrency-safe booking workflow and a service that behaves predictably when dependencies slow down or fail.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
This module has two halves.
- **Contention at database and multi-server scale** (deadlocks, blocking, distributed locks): your M4 locks, grown up.
- **Resilience:** dependencies get slow or fail, and your service must **degrade gracefully** instead of passing the failure on.

### Sessions (in order)
1. **Contention at scale:** a booking system under thousands of users; optimistic vs. pessimistic in practice. 🧪 Load-test the seat claim.
2. **SQL deadlocks:** why they happen, error 1205, lock ordering, short transactions, RCSI, retrying. 🧪 Reproduce a consumer-vs-API deadlock.
3. **Blocking, lock escalation, connection-pool exhaustion.**
4. **Distributed locks (Redis):** leases, owner identity, renewal, safe release, fencing tokens; when not to use a lock.
5. **Timeouts and retries:** backoff with jitter, retry amplification, deadlines and budgets.
6. **Circuit breaker, bulkhead, fallback.** 🧪 Polly v8 / Microsoft.Extensions.Resilience against a flaky dependency.
7. **Load shedding, graceful degradation, backpressure.**

### Done when you can answer
- How do you fix a deadlock without adding more locks?
- Why is a Redis lock alone unsafe, and what do fencing tokens fix?
- Three layers each retry 3 times. What happens to the bottom service?
- What are the circuit breaker's states, and when does it trip and recover?

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

#### Contention at scale (Session 1, 2026-11-17)
- **In plain words:** many requests changing the same row at once; at scale the coordination method decides correctness AND speed.
- **Read-then-write bug:** both read 1, both write 0 → oversell. Fixes close the gap between read and write.
- **Pessimistic (`UPDLOCK`):** lock on read, held until commit; the second request waits (U blocks U). Use when the decision can't fit in one statement. Cost: lock held for the whole transaction → long queue on a hot row.
- **Atomic conditional UPDATE (my words):** same update statement, instead of get-then-update, just update so it's locked right away. Precise: the check lives in the `WHERE` (`AND Available >= 1`), `rowsAffected` says who won, `OUTPUT inserted.X` returns the new value. Lock lasts one statement.
- **Optimistic:** good when conflicts are rare; on a hot row → thousands of conflicts → retry storm.
- **Reducing contention on a hot row:** split into buckets, claim seat rows with `READPAST`, holds with TTL, admission queue / waiting room, Redis pre-check, single writer per key.
- **Quick-check answers (~1.5/3):** Q1 ⚠️ same rowversion ✅ but missed the consequence (all but one fail → retry storm). Q2 ✅ DB-level lock is shared by all servers; C# lock is per process. Q3 ❌ answered with the atomic UPDATE (that IS the contention); needed buckets / READPAST / queue / Redis pre-check.


#### Buckets for a hot row (Session 1 follow-up)
- **My words:** instead of 20,000 requests waiting on one row and then finding the seats gone, split into buckets so requests spread out and each grabs from one.
- **Precise:** buckets split the SEATS (10 × 10); requests pick a bucket at random (~2,000 each); a bucket is a counter, not specific seats; inside a bucket requests still take turns, but 10 rows run in parallel. Losers still reach the DB → add a Redis `DECR` pre-check or waiting room in front. Code: `src/M13.Concurrency/BucketedInventory.cs`.

#### SQL deadlocks (Session 2, 2026-11-17)
- **In plain words:** two transactions each hold a lock the other needs (a cycle); SQL Server kills one (victim, error 1205).
- **Blocking vs. deadlock:** blocking = a line that drains (one waits, the other finishes); deadlock = a cycle that never drains. Two updaters on ONE row = blocking, not deadlock.
- **Shapes:** opposite order (transfer A→B vs. B→A; API Orders→Inventory vs. consumer Inventory→Orders), S→X conversion (fix: `UPDLOCK` on the read), missing index → scans, reader vs. writer (fix: RCSI).
- **Fixes without more locks:** consistent lock order (lower Id first), right indexes, short transactions, RCSI, single statements, retry the WHOLE transaction on 1205 with backoff (safe only if nothing outside the DB happened inside it). Find them in `system_health` XE.
- **Lock types recap:** optimistic takes no locks (never blocks, fails at write); pessimistic locks at the READ and holds until COMMIT (that's what blocks); atomic UPDATE locks for one statement.
- **Quick-check answers:** Q1 ❌ said "same row → victim" (that's blocking) and mixed up optimistic/pessimistic; corrected. Q2/Q3 skipped (see open questions).


#### Blocking, lock escalation, pool exhaustion (Session 3, 2026-11-17)
- **One story:** a long transaction blocks → >~5,000 locks escalate to a TABLE lock → every blocked request holds a pooled connection → pool exhausted → timeouts everywhere, SQL CPU low.
- **Diagnose:** `sys.dm_exec_requests` (`blocking_session_id`) → follow to the head blocker (often a sleeping session with an open transaction); `sp_WhoIsActive`.
- **Fix:** batch big writes (`UPDATE/DELETE TOP (2000)` loop, sequential, pause, off-peak), short transactions, index the WHERE, RCSI, `await using` + `XACT_ABORT ON`. Pool exhaustion is a symptom: raising Max Pool Size just adds more waiters behind the same blocker.
- **Quick-check answers (~5/10):** Q1 ⚠️ leaked connections (valid) but first check blocking chain / head blocker. Q2 ⚠️ batching ✅; missed lock escalation (table lock blocks unrelated rows); staging table overkill; parallel batches make it worse. Q3 ⚠️ restated the symptom; answer = more connections all block behind the same head blocker and add SQL load.


#### Distributed locks (Session 4, 2026-11-17)
- **In plain words:** a lock all servers can see (Redis, SQL, etcd) so only ONE server runs a given job at a time; the others skip. It doesn't split work.
- **Redis recipe:** `SET key <token> NX PX 30000`; release with a Lua "delete only if value == my token"; renew long leases with the same token check.
- **Example (exchange-rate refresh):** NX → one caller per minute; PX → a crashed holder doesn't block forever; expiry → a SLOW holder can lose the lease while still working (two servers at once); token-checked release → a late server can't delete another's lock; fencing token (INCR) checked by the DB (`WHERE Fence < @token`) → the stale writer is rejected.
- **Efficiency vs. correctness:** duplicate work OK → single Redis lock; corruption/money at stake → fencing, or avoid the lock (DB constraints, idempotency).
- **Big jobs (3M rows):** don't hold one lock for hours; claim batches with `UPDLOCK, READPAST` (rows are the lock, sweeper for stuck `Processing`) or lock-to-plan + queue-to-spread.
- **My question → answer:** if SQL already coordinates 3M rows, why Redis for small jobs? Often you don't need it: `sp_getapplock` (auto-released on disconnect) or a unique `JobRuns(JobName, RunMinute)` row. Redis/etcd when there's no shared DB, hot-path locks, long non-DB work, leader election, cross-system resources.
- **Quick check skipped** (see open questions).


#### Timeouts and retries (Session 5, 2026-11-17)
- **Timeouts:** every remote call needs one (HTTP default 100 s is a trap); set from p99 + margin; a slow dependency is worse than a dead one because it holds threads/connections.
- **Retry only transient errors** (timeouts, 503, 429 + `Retry-After`, 1205), never 400/401/403/404/409; non-idempotent POST needs an idempotency key (timeout = unknown outcome).
- **Backoff + jitter, cap attempts (2–3).**
- **Amplification:** 3 layers × (1+3) attempts = 4×4×4 = 64 calls at the bottom. Fix: retry at ONE layer, retry budget (~10–20%), circuit breaker.
- **Deadlines:** pass the remaining budget down (`CancellationToken`, linked CTS + `CancelAfter`); stop when it's gone.
- **Quick-check answers (~8/10):** Q1 ✅ retry at the layer closest to the failure (number is 64 → 4, not 16; add budget/breaker). Q2 ✅ slow calls pile up and exhaust resources (a DOWN dependency fails fast; it doesn't give bad data). Q3 ✅ classify transient vs. permanent; retry the same POST with the same idempotency key.


#### Circuit breaker, bulkhead, fallback (Session 6, 2026-11-17)
- **Circuit breaker (MVP):** a failure counter + state + timer in front of a dependency. CLOSED = call normally; too many failures → OPEN = don't call at all, fail instantly (fallback); after the break → HALF-OPEN = ONE trial call; success → CLOSED, failure → OPEN again.
- **Why:** your service stays fast (no 500 ms timeout per call while it's broken) and the dependency gets zero load to recover. Half-open (not straight to closed) avoids slamming a still-broken service with full traffic.
- **Bulkhead:** cap in-flight calls per dependency (concurrency limiter) so a SLOW dependency can't eat all threads/connections. **Fallback:** good-enough answer (cache, defaults) for non-critical reads; never fake success for critical writes (payments → honest 503 or queue + idempotency key).
- **CancellationToken:** a "stop, not needed anymore" signal passed DOWN to the work (cooperative); sources: `CancelAfter` (timeout), `RequestAborted` (client gone), shutdown. Not "the main thread stops waiting": with await no thread waits; the work itself stops and frees connections. Forgetting to pass it on is the common bug.
- **My explanation (~8.5/10):** we use a circuit breaker to handle slow dependencies; it's a counter that decides whether further calls proceed. We use it with a retry policy: transient → retry a few times with growing gaps; persistent → don't retry. If requests keep failing, the breaker opens so subsequent requests aren't made to the slow dependency. A fallback returns graceful info. **Refinements:** it counts every failed call (not just retries) against a threshold; recovery = half-open with ONE trial call after the break; backoff + jitter. Retry handles a blip, the breaker handles an outage, the fallback is what the user sees.
- **Code:** `src/M13.Concurrency/Resilience.cs` (Polly v8 pipeline: total timeout → bulkhead → retry → breaker → attempt timeout; fallback in the typed client).
- **Quick-check answers:** Q1 ❌ said it retries while open; corrected: open = no call, instant failure + fallback. Q2 ⚠️ "retry after 30 s"; key part = only ONE trial call. Follow-up ✅ open-circuit calls aren't retried (add: fail instantly + fallback).


#### Load shedding / graceful degradation (Session 7): skipped by choice
- One-liners only: load shedding = reject extra requests fast (503/429, concurrency limiter) so accepted ones succeed; graceful degradation = turn off non-essential features (flags, fallbacks) so the core keeps working. Rate limiting = per-client fairness (M4); load shedding = total server capacity.


#### M13 "done when" (2026-11-17)
- Q3 ✅ retries pile up at the bottom (4×4×4 = 64 per request); retry only at the layer closest to the failure (+ budget/breaker).
- Q4 ⚠️ states named ✅; transition wrong: max failures → OPEN (fail fast, no calls), THEN after the break → HALF-OPEN (one trial) → CLOSED or back to OPEN.
- Q1 (deadlock without more locks) and Q2 (Redis lock + fencing) not answered → CP2.


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
