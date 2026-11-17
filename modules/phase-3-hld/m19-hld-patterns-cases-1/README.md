# M19 — HLD patterns and case studies I

> Plan: `Senior_Engineer_Interview_Plan.html#m19` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Explain the 7 patterns, whiteboard both worked examples (100k users, 10k databases), and write up each case.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
Most HLD questions are **combinations of 7 recurring patterns**. Learn each pattern on its own, then practise spotting and combining them in timed case studies: a 45-minute attempt, then a 45-minute review against a reference design.

### Sessions (in order)
1. **Pattern: real-time updates.** Polling, SSE, WebSockets, pub/sub fan-out, connection gateways.
2. **Pattern: contention.** Atomic conditional updates, optimistic and pessimistic locking, holds with TTL, queues.
3. **Pattern: multi-step processes.** Saga, workflow engines (Durable Functions, Temporal), state machines.
4. **Pattern: scaling reads.** Cache, read replicas, CDN, denormalised read models.
5. **Pattern: scaling writes.** Sharding, batching, sharded counters, queues to absorb bursts.
6. **Pattern: large blobs.** Presigned URLs, chunked and resumable uploads, claim-check.
7. **Pattern: long-running tasks.** 202 with a job ID, job and chunk tables, worker pools, checkpoints, progress, DLQ.
8. **Worked example:** one change applied to 100k users (Azure).
9. **Worked example:** one schema change across 10k tenant databases.
10. **Cases, one per session:** URL shortener, WhatsApp, Netflix/YouTube, live streaming, Uber, Yelp, Twitter, Dropbox, live comments, non-AI chat over company data. 🧪 Each one: a timed attempt with an Excalidraw diagram saved in `designs/`, then a review.

### Done when you can answer
- Given a new prompt, can you name the patterns it needs within the first 5 minutes?
- Contention: holding a seat vs. claiming a count across dates. Which update for each?
- When do you return 202 with a job ID instead of doing the work in the request?
- How do you upload a 5 GB file without sending it through your API servers?

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
