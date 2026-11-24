# Deep dive 1 (backend): on-prem search → Azure AI Search

A 20-minute walkthrough of your own system, like an HLD round where you're the expert. The interviewer pushes on "why", scale, cost, failures and alternatives. The 3-minute STAR version is ST8 in [004-ownership-impact](004-ownership-impact.md). Back to [001-start-here](001-start-here.md).

**Done when:** you can give it in 20 minutes with a diagram, and it survives five levels of "why?".

## Structure (fill every ❓)
| Part | Time | What to cover | Known (resume / notes) |
|---|---|---|---|
| 1. Problem and baseline | 2 min | Who searched, for what, how slow, how much data | Poorly performing on-prem wildcard search over large text fields. ❓ baseline latency (p50/p95), data size, users |
| 2. Goal and constraints | 1 min | Target latency, compliance (health data), budget, team | ❓ |
| 3. Options considered | 3 min | Tune the existing search, Solr, Azure AI Search, Azure Data Factory for sync | Solr Cloud on OpenShift was also run (resume); the Solr vs. Azure decision (ST9) |
| 4. Architecture | 5 min | Diagram: source DB(s) → sync → index → API → UI | Custom sync tool instead of Azure Data Factory (notes). ❓ draw it |
| 5. Hardest part | 4 min | Keeping one search document built from ~10 tables consistent under concurrent events | Multi-table event ordering (notes). ❓ how: versions? last-write-wins? rebuild the whole document per event? |
| 6. Re-index | 1 min | Vector / NLP re-index | ❓ which model, how often, how you avoided downtime (alias / blue-green index?) |
| 7. Validation | 2 min | Load tests: tool, load, results | >98% latency reduction, sub-second, load-tested. ❓ tool, requests/s, before vs. after |
| 8. What broke, what I'd redo | 2 min | Honest surprises | ❓ |

## "Why" ladders to prepare (each goes five levels deep)
- Why was wildcard search slow? → why didn't an index fix it? → why not full-text search in SQL Server? → why a separate search engine? → why Azure AI Search over Solr?
- Why a custom sync tool? → why not Azure Data Factory? → how do you know the index matches the database? → what if an event is lost? → how do you rebuild from scratch?
- How do you order events across tables? → what if two updates race? → what if one is delayed? → is the index eventually consistent? → how stale can it be, and who noticed?

## Follow-ups (from the plan)
- **Scale 10×:** partitions and replicas in Azure AI Search; the sync throughput; the database load from rebuilding documents.
- **Cost:** the search tier and its cost vs. the old servers.
- **Monitoring:** index lag, failed syncs, query latency.
- **Security:** health data (PHI) in the index; who can search what (security trimming).
- **Alternatives:** Elasticsearch / OpenSearch, Postgres full-text search, Cosmos DB integrated search.

Related concepts: M7 (search engines, inverted index), M10–M11 (event ordering, outbox), M22 (vector search).
