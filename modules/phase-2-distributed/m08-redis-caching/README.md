# M8 — Redis and distributed caching

> Plan: `Senior_Engineer_Interview_Plan.html#m8` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Measured latency/throughput differences and a written explanation of consistency and invalidation trade-offs.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
Your M4 LRU cache, moved **out of the process** so every server shares it. A cache makes reads fast and cheap, but every cache is a **second copy of the data**. The hard parts are keeping that copy correct (invalidation) and surviving when the cache fails or empties all at once.

### Sessions (in order)
1. **Why a distributed cache:** in-process vs. Redis, latency numbers, hit ratio as the key health metric.
2. **Caching patterns:** cache-aside, read-through, write-through, write-around, write-behind. 🧪 Cache-aside in ASP.NET Core with Redis.
3. **Expiry and eviction:** TTL, Redis's approximate LRU/LFU, memory limits.
4. **Redis data structures and atomic operations:** strings, hashes, sets, sorted sets (leaderboards), `INCR`, Lua scripts.
5. **Invalidation and consistency:** update the DB then delete the cache entry; the races that remain; stale reads.
6. **Failure modes:** stampede (dogpile), penetration, avalanche (mass expiry), hot keys. 🧪 Cause a stampede, then fix it (single-flight lock, jittered TTL).
7. **Distributed rate limiting:** your token bucket as a Redis Lua script. 🧪
8. **When Redis is down:** fallback, timeouts, circuit breaker, restart and failover. 🧪 Measure DB-only vs. cached throughput.

### Done when you can answer
- Cache-aside vs. write-through: what are the trade-offs?
- A hot key expires and the database falls over. What happened, and how do you prevent it?
- Why delete the cache entry after a write instead of updating it? What race is still left?
- What happens to your app when Redis goes down?

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
