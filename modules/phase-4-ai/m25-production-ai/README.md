# M25 — Production AI, LLM security, and AI system design

> Plan: `Senior_Engineer_Interview_Plan.html#m25` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] For each AI design: requirements, architecture, retrieval, eval plan, cost model, failure modes, security.

## Phase 4 builds: three code-only projects, built in order (learner, 2026-11-22)
Code to read, not to run: each project builds with `dotnet build`; no Docker, databases or tools to start. One small class per concept. New topics are learned **while building**, when they show up in the code. The rest of M25 is learned this way instead of as separate sessions.

### 1. RAG service: `modules/phase-4-ai/src/Phase4.Rag` (M22 + M23)
- **Ingestion:** `POST /documents` → `Chunker` (split by headings + size, heading prefix, overlap) → embed → `ChunkStore` (Postgres/pgvector SQL: tenant, groups, citation fields; content hash to skip unchanged; replace a document's chunks in **one transaction**).
- **Freshness:** `DELETE /documents/{id}` (chunks + cache), `PUT /documents/{id}/permissions` (update `allowed_groups`, no re-embedding), `embedding_v1` / `embedding_v2` columns with a config switch for blue/green model upgrades.
- **Query:** `POST /ask` → `HybridSearch` (keyword + vector + RRF, filtered by JWT tenant/groups) → `Reranker` (top 20 → top 5, "I don't know" threshold) → `PromptBuilder` (best first, dedupe, token budget, `<source>` tags) → LLM (temperature 0, streamed) → `CitationChecker` (drop invented `[n]`, map to links).
- **Debugging:** a `RagTrace` per question recording every stage (keyword / vector / RRF ids, rerank scores, prompt sources, tokens, prompt version, model, answer, citations), plus `GET /ask/{traceId}/trace` to see which stage lost the right chunk.
- **M23 additions:** `PromptBuilder` puts fixed parts first (system prompt, tools) and changing parts last (sources, question, time) so prompt caching hits; `RagTrace` logs input / cached / output tokens and the cost per call.

### 2. Agents: `modules/phase-4-ai/src/Phase4.Agents` (M24)
- **Support agent** (agent vs. workflow): typed least-privilege tools (read vs. write, enums, owner checks with the user's identity, idempotency keys), stop conditions (max steps, repeat detection, timeout), per-task budgets (steps, tokens, time), approval tiers in code (auto for reads / small refunds, human approval above a limit).
- **Workflow next to it:** a fixed ticket pipeline (summarise → classify → route) to contrast with the agent.
- **Multi-agent:** orchestrator–worker (parallel workers with small contexts, returning summaries) and evaluator–optimizer (rubric, max 3 rounds).
- **Memory + context engineering:** a per-step `ContextBuilder` (budget, only needed tools, summary of old turns, trimmed tool results); long-term memory of user-stated facts only (scoped per user); shared state with a version check.
- **Traces:** each step's tool, arguments, result, tokens and cost.
- **MCP:** the order tools also exposed as an MCP server.
- **One eval example:** golden cases, tool + answer checks, repeated trials, a pass/fail gate.

### 3. Production AI and security: `modules/phase-4-ai/src/Phase4.ProductionAi` (M25)
- **Cost/latency:** semantic cache (key = question vector + user groups + doc versions), routing easy steps to a smaller model, context trimming, cost per resolved task.
- **Guardrails:** input/output checks, PII redaction, a moderation step (threshold + human review band).
- **Security:** prompt injection defences (sources as data, no secrets in the system prompt, tool tiers), cross-tenant isolation, authorisation at retrieval time AND action time, no data exfiltration through tools.
- **LLM-generated filters / text-to-SQL:** validate against a schema/allowlist, never trust a generated filter for authorisation (security trimming server-side), read-only login + allowlisted views + row limits.
- **Observability + incident drill:** per-call metrics; a "prompt change made it worse" flow: roll back, reproduce, add a regression eval, canary the fix.
- **System design:** the AI chat over company documents ([design](designs/ai-chat-company-docs/ai-chat-company-docs.md)) and a hybrid router: deterministic flows for transactional intents, AI for open questions, a human for the rest.

_Removed by the learner on 2026-11-22: the LLM gateway (provider routing, per-tenant quotas, key management, metering)._

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
