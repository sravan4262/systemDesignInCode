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
