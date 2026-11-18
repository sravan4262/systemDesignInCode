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

#### Consistency models (Session 1, 2026-11-18)
- Strongest → weakest: linearizable (one-copy illusion, latest write everywhere) → sequential (same order for all) → causal (dependent writes in order: no reply before the post) → read-your-writes / monotonic reads (session guarantees) → eventual (agree someday, any order).
- Linearizable (one item across replicas) ≠ serializable (transactions as if one at a time, M6); strict serializable = both.
- Mapping: SQL primary = linearizable; async replica / Redis = eventual; ReadWriteRouting.cs = read-your-writes; Cosmos Strong / Session (default: RYW + monotonic per session) / Consistent prefix / Eventual.
- Choose the WEAKEST model that fixes the problem: money/stock/unique names → linearizable; comments/chat → causal; own edits → session; counts/feeds → eventual.
- **Quick-check answers:** Q1 ⚠️ 5/10 said strong; minimal = CAUSAL. Q2 ✅ 8/10 session (read-your-writes; M6 = route that user's reads to the primary after a write). Q3 skipped.


#### Replication and quorums (Session 2, 2026-11-18)
- N copies, write done after W confirmations, read asks R; R + W > N → read overlaps the latest write (pick newest version). N=3: W=2/R=2 quorum tolerates 1 down; W=1/R=1 fast but stale (eventual). Cassandra ONE/QUORUM/ALL per query.
- Catch-up: read repair, hinted handoff, sloppy quorum, anti-entropy with Merkle trees. Leader-based (one writer, failover pause, no conflicts) vs. leaderless (any W nodes, stays up, concurrent-write conflicts). Cosmos: quorum inside a region; consistency level across regions.
- Quick check skipped (open: W=1/R=1 stale? yes, may hit a node without the write; N=5 → W=R=3).


#### Consistent hashing (Session 3, 2026-11-18)
- Lab (`src/M17.Theory`, 100,000 keys, 4 → 5 nodes): modulo moved 79.9%, hash ring moved 19.8% (ideal 20%); 100 virtual nodes each → 18–22% per node.
- Ring = servers and keys hashed onto one circle; key → first server clockwise; adding a server moves only its slice (~1/N). Virtual nodes = many points per server → even spread, a leaving server's keys spread across all.
- Data structure: sorted (position → server) list + binary search for the first position ≥ hash (O(log n)), wrap around; small, in memory, copy-on-write on changes. Fixed slots (Redis 16,384, M6 buckets) = array lookup O(1).
- Strategies: range (range queries, hot last range) · hash modulo (even, ~80% move on resize) · consistent hashing (even, ~1/N move) · directory (full control, lookup + critical table; best for thousands of tenants; hash → buckets → directory of buckets for billions of keys). Tenant-based = choice of KEY, placed by hash or directory. Hashing is also used for dictionaries, integrity (JWT, ETags), security (PKCE, passwords), dedup, Bloom filters.
- **My explanation (~7.5/10):** range, lookup, hash, consistent explained in order. Fixes: collisions are NOT the hash problem (keys sharing a shard is the point); the problem is resizing (~80% move). Servers are HASHED onto the circle (not sharded); key goes to the FIRST server clockwise; add the why (~1/N moves, virtual nodes).


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
