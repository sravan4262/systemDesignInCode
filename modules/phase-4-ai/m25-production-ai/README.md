# M25 — Production AI, LLM security, and AI system design

> Plan: `Senior_Engineer_Interview_Plan.html#m25` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] For each AI design: requirements, architecture, retrieval, eval plan, cost model, failure modes, security.

## Practical build (end of Phase 4)
One **code-only** build (it compiles; no Docker or tools to run) at the end of Phase 4: **AI chat over company documents** = the M22 RAG service + whatever M23–M25 add. Design drafted 2026-11-21 in [`designs/ai-chat-company-docs/`](designs/ai-chat-company-docs/ai-chat-company-docs.md); revisit it before building. While learning M23, M24 and M25, add anything worth building to the backlog below.

### Base scope (from M22), one small class per concept
- **Ingestion:** `POST /documents` → `Chunker` (split by headings + size, heading prefix, overlap) → embed → `ChunkStore` (Postgres/pgvector SQL: tenant, groups, citation fields; content hash to skip unchanged; replace a document's chunks in **one transaction**).
- **Freshness:** `DELETE /documents/{id}` (chunks + cache), `PUT /documents/{id}/permissions` (update `allowed_groups`, no re-embedding), `embedding_v1` / `embedding_v2` columns with a config switch for blue/green model upgrades.
- **Query:** `POST /ask` → `HybridSearch` (keyword + vector + RRF, filtered by JWT tenant/groups) → `Reranker` (top 20 → top 5, "I don't know" threshold) → `PromptBuilder` (best first, dedupe, token budget, `<source>` tags) → LLM (temperature 0, streamed) → `CitationChecker` (drop invented `[n]`, map to links).
- **Debugging:** a `RagTrace` per question recording every stage (keyword / vector / RRF ids, rerank scores, prompt sources, tokens, prompt version, model, answer, citations), plus `GET /ask/{traceId}/trace` to see which stage lost the right chunk.

### Backlog: additions from M23–M25
_Add an item whenever a topic is worth showing in the build._
- **M23:** _(none yet)_
- **M24:** _(none yet)_
- **M25:** _(none yet)_

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
