# M4 — Concurrent LLD and machine coding

> Plan: `Senior_Engineer_Interview_Plan.html#m4` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Concurrency-tested implementations, each produced within a 60–90 minute machine-coding window.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### M4 summary (concepts + code)
- **LRU cache** (`LruCache.cs`): Dictionary (find O(1)) + LinkedList (order, move/remove O(1)); Get also moves to front, so one exclusive `lock` (RW lock gains nothing). Without the lock: capacity check races, map and list drift apart or corrupt. Scale-out: striped shards (LRU per shard).
- **Eviction policies:** LRU (recency), LFU (frequency; O(1) with freq → list + minFreq), FIFO (insertion order), TTL (store ExpiresAt, lazy check + sweep, no timer per key).
- **Rate limiters** (`IRateLimiter`, Strategy): token bucket (bursts, lazy refill, `TokenBucketRateLimiter.cs`), sliding window counter (2 counters, no boundary burst, `SlidingWindowRateLimiter.cs`), fixed window (boundary burst), leaky bucket (queue drained at fixed rate, for protecting a downstream). `KeyedRateLimiter` = one limiter per client; GetOrAdd factory may run twice (harmless here). Per process only; across servers use Redis.
- **Bounded queue / Channels (concept only):** in-process hand-off with backpressure (Wait / Drop modes); one channel per subscriber for fan-out; not durable → RabbitMQ for business messages, Channels inside a process (telemetry, batching, fan-out to workers).
- **KV store with transactions** (`KeyValueStore.cs`): committed dict + stack of draft dicts; Get walks top → committed; tombstone for delete; Rollback pops; Commit merges all bottom → top.
- **Primitives** (`Primitives/`): `lock` = threads in one process; named `Mutex` = processes on one machine sharing a resource (OS object, by name, voluntary, read-change-write all inside); `ReaderWriterLockSlim` = shared read / exclusive write (like SQL S/X/U), writer priority prevents starvation; only for read-heavy truly read-only data. Across servers: SQL/Redis locks.
- **Thread-safe logger (concept only):** callers enqueue to a bounded Channel, ONE background writer writes in batches (no file lock needed); drop when full; drain on shutdown; audit logs must be durable instead.

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
