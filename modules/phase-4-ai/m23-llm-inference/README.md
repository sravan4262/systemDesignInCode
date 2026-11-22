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
- **Prompt caching vs. a response cache:** prompt caching saves only the model's processing ("notes") of an **exact** prompt prefix (system prompt, tools, few-shot examples, earlier chat turns); on a hit the model loads the notes instead of re-processing, still uses that text, still counts it in the context window, bills it at a discount, and **writes a fresh answer every time**. Reusing answers = a cache I build (exact response cache, or a semantic cache in M25).
- **Quick-check answers:** 4 s before anything appears → prefill: shorter prompt (fewer chunks, history summary) + prompt caching with a fixed-first order. `Current time` at the top → it changes every call, so the prefix differs at token 1 and nothing after it is cached; move it to the end. **Output costs ~4–5× more per token** (decode, one at a time); per call, cut whichever side dominates: RAG (3,000 in × 1 vs. 300 out × 4) → input is the bigger bill; long reports (500 in vs. 2,000 out × 4) → output is.

#### Prompting → RAG → fine-tuning (the adaptation ladder)
- **In plain words:** Prompting = tell it what to do; RAG = give it the facts at question time; fine-tuning = train a copy on examples so it learns a behaviour, style or narrow task. Climb one step at a time, only when the step below isn't enough.
- **Problem it solves:** "Fine-tune so it knows our policies" is the classic mistake: fine-tuning teaches behaviour, not facts; changes mean retraining; no citations, no permissions.
- **Analogy:** A new employee: instructions (prompting), the handbook to look things up (RAG), months of training until habits stick (fine-tuning).
- **Diagram:** wrong format/tone → prompting (instructions, few-shot examples, structured output, temperature 0); missing or changing facts → RAG; prompting works but is too long/expensive at scale, or one narrow task at huge volume → fine-tuning (often a small model). Often combined: RAG for facts + prompting for format.
- **Variations / alternatives:** Few-shot prompting (often 80% of fine-tuning for free), tool calling for live data, a fine-tuned small model as a cheap classifier/extractor, or simply a stronger base model.
- **When not to use it (fine-tuning):** For changing facts / citations / permissions (use RAG); before trying prompting + evals; with fewer than a few hundred good examples; without budget to retrain when the base model changes.
- **Quick-check answers:** "Knows our HR policies" → RAG: policies change (re-index in minutes vs. retrain) and RAG gives citations + permission filtering. Inconsistent format → prompting first (format instructions, 2–3 examples, structured output, temp 0); the same model alone doesn't fix it. 10M tickets/day into 12 categories → fine-tune a small model: narrow stable task, huge volume, no long prompt needed, training data from the large model's labels (human-checked); evals vs. the large model; route low-confidence tickets to the large model or a human.

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
