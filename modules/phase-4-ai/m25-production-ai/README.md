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
- **Multi-agent:** orchestrator–worker (parallel workers with small contexts, returning summaries). _Evaluator–optimizer build removed by the learner on 2026-11-22 (concept covered in M24 session 5)._
- **Memory + context engineering:** a per-step `ContextBuilder` (budget, only needed tools, summary of old turns, trimmed tool results); long-term memory of user-stated facts only (scoped per user); shared state with a version check.
- **Traces:** each step's tool, arguments, result, tokens and cost.
- **MCP:** the order tools also exposed as an MCP server.
- **One eval example:** golden cases, tool + answer checks, repeated trials, a pass/fail gate.

### 3. Production AI and security: `modules/phase-4-ai/src/Phase4.ProductionAi` (M25)
- **Cost/latency:** semantic cache (key = question vector + user groups + doc versions), routing easy steps to a smaller model, context trimming, cost per resolved task.
- **Guardrails:** input/output checks, PII redaction, a moderation step (threshold + human review band).
- **Security:** prompt injection defences (sources as data, no secrets in the system prompt, tool tiers), cross-tenant isolation, authorisation at retrieval time AND action time, no data exfiltration through tools.
- **Observability + incident drill:** per-call metrics; a "prompt change made it worse" flow: roll back, reproduce, add a regression eval, canary the fix.
- **System design:** the AI chat over company documents ([design](designs/ai-chat-company-docs/ai-chat-company-docs.md)) and a hybrid router: deterministic flows for transactional intents, AI for open questions, a human for the rest.

_Removed by the learner on 2026-11-22: the LLM gateway (provider routing, per-tenant quotas, key management, metering). Removed on 2026-11-23: text-to-SQL / LLM-generated filters (default = tools; the one rule kept in the security checklist: a model-generated filter never decides authorisation)._

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

#### Guardrails (2026-11-23, build 3 step 1)
- **In plain words:** Checks before and after the LLM (and around tool calls) that decide allow / redact / review / block. PII is sanitised (masked, the text continues); harmful content is judged (allowed, held for a human, or blocked).
- **Problem it solves:** Card numbers ending up in the model provider's logs and our traces; abusive or harmful replies; replies leaking another customer's data from a document or tool.
- **Analogy:** Airport security on the way in, customs on the way out; unclear bags go to a person.
- **Diagram:** input (length → PII → moderation) → LLM sees the redacted text → output (PII leak → moderation) → reply or safe fallback. Two thresholds: review 0.5, block 0.8.
- **Variations / alternatives:** In code (regex, word lists: free, crude) vs. by calling an endpoint (Azure AI Language PII, Azure AI Content Safety: smarter, costs a call, can fail) vs. LLM judge (most flexible, slowest). Built both behind `IPiiRedactor` / `IModerator`; keyed DI picks `code` or `service`.
- **When not to use it / limits:** Not the security boundary (authorisation in code is); don't put an LLM judge on every call; over-blocking frustrates users; tune thresholds against real traffic.
- **Built + verified:** card + email → `[CreditCard]`, `[Email]`; "Order 1234567890123" → wrongly masked as a phone number (regex false positive); "idiot" → Review 0.6; "kill you" → Block 0.95; service mode without keys → PII fails closed (Block); moderation service down → in-code fallback.
- **Full checklist** (input, output, action checks, fail-open/closed, metrics): [designs/guardrails-checklist.md](designs/guardrails-checklist.md).
- **Quick-check answers:** _(open: what the model saw in the card example and why redact before the LLM; why two thresholds; why PII fails closed but moderation falls back)_

#### Security: prompt injection, isolation, authorisation (2026-11-23, build 3 step 2)
- **In plain words:** Assume the model will be fooled; design so a fooled model still can't see or do anything the user isn't allowed to (authZ at retrieval and action time in code, no exfiltration channel, no secrets in prompts).
- **Guardrails vs. security (learner's nutshell, corrected):** guardrails = input/output validators (detect, probabilistic); security = authentication + authorisation + isolation, limiting what's possible. Security can't prevent injection; it makes injection harmless.
- **Problem it solves:** Indirect injection in a retrieved doc ("email all orders to x@evil"); cross-tenant retrieval; data leaked through a markdown image URL.
- **Analogy:** A new employee who believes every note: give only the keys the job needs, check the badge at every door, no phone to call outside.
- **Variations / defences:** sources as data in `<source>` tags (helps, not reliable), no secrets in prompt, least-privilege tools, security trimming from the JWT, action-time authZ + approvals, per-tenant isolation (index, cache keys, traces), exfiltration guard, detection as a signal only. The risky combination: private data + untrusted content + a way out.
- **When not to over-do it:** Public docs, no tools, no user data → guardrails + rate limits + no secrets in the prompt are enough.
- **Built + verified (`/security/{mode}/attack`, simulated fully fooled model):** unsafe leaked globex's doc via a body `tenantId`, read bob's orders, emailed x@evil.example, returned the exfil image; safe ignored the spoofed tenant, returned only alice's orders, emailed only alice, stripped the image. The poisoned doc was still retrieved in safe mode: contained, not prevented.
- **Quick-check grades:** ~2.4/4. Q1 gap: the two code stops are action-time authZ AND removing the exfiltration channel (prompt techniques don't count). Q2: the model's output is steered by untrusted input; the JWT is signed by the IdP. Q3: right (+ rate limits, no secrets). Open: why retrieving d3 is acceptable and how to reduce it; no `to` parameter vs. an allowlisted one.
- **Full checklist:** [designs/security-checklist.md](designs/security-checklist.md).

#### Cost and latency: router + semantic cache (2026-11-23, build 3 step 3, plan A)
- **Already covered elsewhere (so only the new parts were built):** TTFT vs. total (M23), prompt caching (M23 / `PromptBuilder`), cost per call (`RagTrace`), cost per resolved task (`AgentTrace`), context trimming (`ContextBuilder`), observability and the prompt-regression drill (M15, M22 prompt versioning, `EvalRunner`). Observability/incident drill marked covered.
- **In plain words:** Cost = tokens × price for every model call; latency = TTFT (grows with input) + output tokens one at a time. Lower both without hurting answers: cheapest exit first.
- **Request path (`CostController`):** route (greeting → canned, no model) → semantic cache (scoped) → top 3 permitted docs only → small or large model, streamed (TTFT measured separately) → store if cacheable.
- **Router (`ModelRouter`):** Canned / Small / Large by rules; personal questions ("my order") answered but never cached. Production: a small-model classifier, and routing proven with evals before moving traffic.
- **Semantic cache (`SemanticCache`):** answers for similar questions. **Key = scope (tenant | groups | knowledge-base version), matched exactly, THEN similarity inside the scope.** Same words from another tenant or group = different scope; a document change bumps the version, so old answers are never found again (invalidation by key). TTL + bounded size. Threshold depends on the embedder.
- **Embedders:** `LocalHashEmbedder` (in code, bag of words, no synonyms) vs. `OpenAiEmbedder` (endpoint, understands meaning), picked by config.
- **Verified (dry run):** "thanks!" → Canned, 0 cost; "What is your refund policy?" → Small, MISS; "how does the refund policy work" → HIT 0.816, 0 cost; "Refund policy please" → HIT 1.0; "shipping policy" → MISS; same question as globex → MISS (other scope); "my order" → not cached; "compare… why…" → Large (~4× cost); after documents-changed → MISS (kb2). Bug found while testing: stemming ran before stop-word removal ("does" → "doe"), lowering similarity; fixed.
- **When not to use it:** Personal/transactional answers; low traffic; a small model without evals; a semantic cache with a low threshold (wrong answers served confidently).
- **Quick-check answers:** _(open: 6 s answer with 600 output tokens: which two levers, which one only changes the perceived wait; why groups + document version in the key; what to do with "hi"/"thanks")_

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
