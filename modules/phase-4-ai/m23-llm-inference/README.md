# M23 — LLM internals for model users (light, one session)

> Plan: `Senior_Engineer_Interview_Plan.html#m23` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A one-page fine-tune vs. RAG vs. prompting memo, and a short explanation of what drives LLM cost and latency.

## Scope (trimmed 2026-11-21: the learner uses models via APIs; doesn't build, train or host them)
- **Keep (light):** tokens and context cost; prefill vs. decode + KV cache (why TTFT grows with prompt length, why prompt caching saves money); prompting → RAG → fine-tuning and when each is worth it; self-host vs. API in one line; LLM as a classifier (moderation thresholds, precision vs. recall, human review).
- **Removed:** attention math, inference engine internals (vLLM, PagedAttention, continuous batching, speculative decoding), quantization, GPU memory math and the serving lab, fine-tuning methods (SFT, LoRA, DPO, distillation), recommendation and ranking.

## Learning plan

### Big picture (read first)
You call models through an API; you don't build or host them. This module gives you just enough of what happens inside a model call to explain **cost and latency**, and to choose between **prompting, RAG and fine-tuning**. Four short topics, then the memo.

### Topics (in order)
1. **Prefill vs. decode, KV cache, prompt caching:** why time to first token grows with prompt length, why output is slow and expensive, why caching the fixed part of a prompt saves money.
2. **Prompting → RAG → fine-tuning:** what each one changes and when each is worth it.
3. **Self-hosting vs. an API:** in one line.
4. **LLM as a classifier (moderation):** thresholds, precision vs. recall, a human review queue.

### Done when you can answer
- What drives the cost and latency of one LLM call, and how do you reduce each?
- A team wants to fine-tune so the bot "knows our policies". What do you recommend, and why?
- Your moderation classifier flags 5% of posts and misses some bad ones. How do you set the threshold?

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

#### Prefill vs. decode, KV cache, prompt caching
- **In plain words:** **Prefill** = the model reads the whole prompt at once (parallel) → decides the time to first token (TTFT). **Decode** = it writes the answer one token at a time → decides the total time. **KV cache** = notes on what it has read, so each next token in the SAME call doesn't re-read the prompt. **Prompt caching** = keep those notes for the prompt's beginning ACROSS calls, so the next request with the same start is cheaper and faster.
- **Problem it solves:** Picking the right fix: a slow first word is prefill (shorter prompt, caching); a slow total is decode (shorter answers, smaller model, streaming); paying full price for the same 2,000-token system prompt every call.
- **Analogy:** A translator reads the whole document (prefill), then speaks word by word (decode), using notes (KV cache); if tomorrow's document starts with the same pages, they reuse yesterday's notes (prompt caching).
- **Diagram:** 3,000-token prompt → prefill ~0.3–0.5 s → first token; 300 tokens × ~20 ms ≈ 6 s decode. Cache works only on an **exact prefix**: put fixed parts first (system prompt, tools, fixed docs), changing parts last (sources, question, current time).
- **Variations / alternatives:** Automatic vs. explicit caching (check the provider); a semantic cache (M25) skips the whole call for similar questions; a smaller model speeds up decode.
- **When not to use it:** Short prompts (below the provider's minimum), prompts that change at the start, rare calls (caches expire in minutes).
- **Quick-check answers:** 4 s before anything appears → prefill: shorter prompt (fewer chunks, history summary) + prompt caching with a fixed-first order. `Current time` at the top → it changes every call, so the prefix differs at token 1 and nothing after it is cached; move it to the end. **Output costs ~4–5× more per token** (decode, one at a time); per call, cut whichever side dominates: RAG (3,000 in × 1 vs. 300 out × 4) → input is the bigger bill; long reports (500 in vs. 2,000 out × 4) → output is.

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
