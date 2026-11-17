# M7 — NoSQL, storage engines, and search

> Plan: `Senior_Engineer_Interview_Plan.html#m7` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A NoSQL model for the capstone's order history and an event-fed search index, compared against the SQL design.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
SQL isn't the only shape a database can take. Learn how databases store data on disk (B-tree vs. LSM-tree), how NoSQL makes you design **around your queries and a partition key**, and how search engines answer "find documents containing these words". The goal is to pick the right store for a job **and defend the choice**.

### Sessions (in order)
1. **Storage engines:** B-tree vs. LSM-tree (memtable, SSTables, compaction, Bloom filters); read, write and space amplification.
2. **Document and key-value modelling:** model around access patterns, denormalise, single-table design.
3. **Cosmos DB in depth:** partition-key choice, hot partitions, RU cost, the 5 consistency levels, indexing policy, change feed, transactional batch. 🧪 Cosmos emulator: a good vs. a bad partition key.
4. **Wide-column (Cassandra):** query-first tables, tunable consistency.
5. **Same query, two stores:** order history in SQL vs. NoSQL. 🧪 Compare latency, cost and consistency.
6. **Search engines:** inverted index, analysers, BM25 scoring, near-real-time refresh, shards and replicas. 🧪 An Elasticsearch container.
7. **Keeping search in sync:** events or change feed → indexer; Solr and Azure AI Search compared.
8. **Choosing a store:** relational vs. document vs. wide-column vs. key-value vs. search, as a decision checklist.

### Done when you can answer
- Why do LSM-trees write faster than B-trees, and what do they give up for it?
- How do you choose a Cosmos DB partition key, and why is a random GUID usually wrong?
- Why not just use `LIKE '%term%'` instead of a search engine?
- Order history, chat messages, product catalogue: which store for each, and why?

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### M7 overview (2026-11-17, all 8 sessions at overview level by choice; no labs)
- **Storage engines:** B-tree (sorted in place; fast predictable reads; SQL Server) vs. LSM-tree (append to memtable → SSTables → compaction, Bloom filters; very fast writes; Cassandra/RocksDB). One-liner: "LSM = sequential appends, suits write-heavy data."
- **Document/key-value modelling:** queries first; one read from one partition, no joins. Embed what's read together and bounded (order lines, address at order time); reference what's large/unbounded/independent (customer, current price). Copy display fields (name/price at purchase). Don't embed 50,000 orders in a customer (2 MB doc limit).
- **Cosmos DB:** partition key = M6 shard key (logical partition ≤ 20 GB, physical partitions managed); random GUID key = every non-point query fans out; hierarchical keys for giant tenants. RU cost model, 429 on hot partitions. 5 consistency levels: Strong, Bounded staleness, **Session (default: read-your-writes + monotonic reads per session)**, Consistent prefix, Eventual. Index everything by default (exclude paths to save write RUs). Change feed; transactional batch only within one logical partition.
- **Wide-column (Cassandra):** one table per query, partition key + clustering key (sort), masterless ring, tunable consistency (QUORUM + QUORUM → R + W > RF), tombstones, bucket big partitions (`chat_id, month`).
- **Same query, two stores:** order history stays in SQL (transactions, reporting); add a Cosmos read model partitioned by `customerId` only at huge scale (CQRS, M10).
- **Search:** inverted index, analysers (tokenise, lowercase, stem, synonyms), BM25 relevance, near-real-time refresh (~1 s, eventually consistent), shards + replicas, facets, vector search. Never the source of truth. `LIKE '%x%'` = full scan, no relevance/stemming/typos.
- **Keeping search in sync:** never dual-write; outbox / CDC / change feed / built-in indexers → indexer with idempotent upserts, version check (ignore older), deletes as events, alias swap for rebuilds, monitor lag.
- **Choosing a store:** start from access patterns + transaction needs; default SQL + cache; NoSQL/search/wide-column only for a named pattern; each extra store = sync path + owner. Order history → SQL; chat → wide-column/Cosmos by `chatId`; catalogue → document/SQL + search.

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
