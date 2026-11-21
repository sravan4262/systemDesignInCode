# M22 — LLM application fundamentals and RAG

> Plan: `Senior_Engineer_Interview_Plan.html#m22` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A tenant-scoped RAG service with citations and a stage-by-stage debugging guide.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
An LLM app is a normal distributed service with one slow, costly, non-deterministic dependency: the model. Sessions 1–3 make a single model call reliable (what goes in, what comes out, what fails). Sessions 4–6 add RAG: give the model the right company text at question time, with citations, and debug it when the answer is wrong. Session 7 builds it in .NET. The M25 design `ai-chat-company-docs` is the end goal these sessions feed.

### Sessions (in order)
1. **One LLM call.** Tokens, context window, sampling (temperature, top-p), model choice, pricing.
2. **Structured output and tool calling.** JSON schema output, function calling; malformed output, refusals, cut-off responses.
3. **Reliability.** Timeouts, bounded retries, streaming, provider rate limits (429), fallback models; versioning prompt + model + schema together.
4. **RAG ingestion.** Chunking, embedding models, vector index basics (exact vs. ANN, HNSW high level, recall vs. latency), metadata filters.
5. **Retrieval.** Hybrid search (BM25 + vectors), Reciprocal Rank Fusion, reranking, context assembly, citations.
6. **Debugging and freshness.** Stage-by-stage RAG debugging; re-indexing, deletions, embedding-model upgrades.
7. 🧪 **Build.** Tenant-scoped RAG service with Microsoft.Extensions.AI / Semantic Kernel 1.x (code samples).

### Done when you can answer
- Why does a longer prompt cost more and respond slower, and what is temperature for?
- The model returns invalid JSON 2% of the time. What do you do?
- A provider returns 429 under load. Walk through your retry and fallback.
- The right document is in the index but the answer is wrong. Debug it stage by stage.
- Why hybrid search over vectors only, and what does RRF do?
- How do you switch embedding models without downtime?

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### One LLM call (tokens, cost, latency)
- **In plain words:** Text goes in, the model writes the answer back one token at a time (a token is about ¾ of a word). I pay for input and output tokens.
- **Problem it solves:** Knowing this stops me sending huge prompts. The model remembers nothing between calls, so a chat resends the whole history every turn, and by turn 40 it's slow and expensive.
- **Analogy:** A taxi meter: pay for distance in (prompt) and out (answer). The context window is the trunk size.
- **Diagram:** prompt (read all at once, fast) → model → answer (one token at a time, slow). Cost = input tokens × input price + output tokens × output price (output is ~4–5× more per token).
- **Variations / alternatives:** Big model for hard answers, small cheap model for rewrite / classify / route.
- **When not to use it:** Exact lookups or calculations, wrong answer not allowed with no human check, needs to be under 50 ms.
- **Quick-check answers:** Fix long chats with the last 10 turns + a summary, and prompt caching. A long prompt mostly delays the *first* word; the answer length drives the *total* wait, so stream it.

#### Temperature
- **In plain words:** A setting (0 to ~1), set once per call and used at every token: how strongly the model sticks to its top guess. Low = predictable, high = varied.
- **Problem it solves:** Facts need the same answer every time; creative tasks need variety. Temperature lets me pick per call.
- **Analogy:** Always ordering my favourite dish (0) vs. sometimes trying something else on the menu (1).
- **Diagram:** Each step: look at prompt + tokens so far → odds for the next token → pick using temperature → repeat. One unusual early pick changes everything after it.
- **Variations / alternatives:** Top-p (only pick from the top guesses adding up to p). Change one, not both. Reasoning models may ignore it.
- **When not to use it:** Don't raise it for facts, extraction, JSON, RAG answers or code (keep 0–0.2). Raise it (0.7–1) only for taglines and brainstorming.
- **Quick-check answers:** Refund-window bot → low (a fact from the doc). 5 subject lines → high (low gives near-copies). Temp 0 with slightly different runs → not broken: tiny server rounding flips near-tie picks, so test with many examples (evals).

#### Structured output
- **In plain words:** I give the model a JSON schema and it must reply in exactly that shape, so code can read it.
- **Problem it solves:** "Reply in JSON" in the prompt breaks 1–5% of the time (fences, chatty text, wrong types, cut off).
- **Analogy:** A form with labelled boxes instead of a free-text letter.
- **Diagram:** prompt + schema → model (temp 0) → validate → typed object. Invalid → retry once with the error → dead-letter. Refusal → its own case.
- **Variations / alternatives:** Prompt-only JSON < JSON mode (valid JSON, any shape) < strict schema (exact shape, use this). Tool calling uses the same mechanism. .NET: `GetResponseAsync<T>` in Microsoft.Extensions.AI.
- **When not to use it:** Answers that go to a person (stream plain text), huge nested schemas (split the call), cases a regex can do.
- **Quick-check answers:** The schema only checks the shape, not the truth: `amount: 1200` passes, so code checks it (is the number in the email? does it match the PO? is it in range?) and evals test it before shipping. **Always check the finish reason**: `length` means cut off (50 tokens for 20 products ≈ 300–400 needed). Raise the limit or ask for fewer per call. A cut-off plain-text answer can look fine, so check every time.

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
