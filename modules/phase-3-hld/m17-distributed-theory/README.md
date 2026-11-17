# M17 — Distributed systems theory

> Plan: `Senior_Engineer_Interview_Plan.html#m17` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Explain Raft, quorums, consistent hashing, and vector clocks from memory, backed by a consistent-hashing implementation.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
The theory underneath the databases and coordination services you use. It covers:
- how nodes **agree** (consensus)
- how copies stay **consistent enough** (quorums)
- how keys **spread** across nodes (consistent hashing)
- how to **order events without trusting clocks**
- small, approximate data structures that save a lot of space

### Sessions (in order)
1. **Consistency models, precisely:** linearizable, serializable, causal, read-your-writes, eventual.
2. **Replication and quorums:** R + W > N, sloppy quorums, hinted handoff, read repair, Merkle trees.
3. **Consistent hashing with virtual nodes.** 🧪 Build it in C#, then measure how many keys move when a node joins or leaves.
4. **Time and ordering:** clock skew, Lamport clocks, vector clocks, hybrid logical clocks; why timestamps can't order events.
5. **Consensus with Raft:** leader election, log replication, terms, commit index; preventing split-brain.
6. **Gossip, failure detectors, CRDTs.**
7. **Probabilistic structures:** Bloom filter, HyperLogLog, Count-Min Sketch.

### Done when you can answer
- Why does R + W > N guarantee you read the latest write?
- Explain Raft leader election in 1 minute.
- Why does consistent hashing use virtual nodes?
- Why can't you order events across servers by timestamp?

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
