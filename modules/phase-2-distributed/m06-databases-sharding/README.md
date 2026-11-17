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
