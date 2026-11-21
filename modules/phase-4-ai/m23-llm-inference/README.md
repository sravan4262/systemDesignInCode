# M23 — LLM internals for model users (light, one session)

> Plan: `Senior_Engineer_Interview_Plan.html#m23` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A one-page fine-tune vs. RAG vs. prompting memo, and a short explanation of what drives LLM cost and latency.

## Scope (trimmed 2026-11-21: the learner uses models via APIs; doesn't build, train or host them)
- **Keep (light):** tokens and context cost; prefill vs. decode + KV cache (why TTFT grows with prompt length, why prompt caching saves money); prompting → RAG → fine-tuning and when each is worth it; self-host vs. API in one line; LLM as a classifier (moderation thresholds, precision vs. recall, human review).
- **Removed:** attention math, inference engine internals (vLLM, PagedAttention, continuous batching, speculative decoding), quantization, GPU memory math and the serving lab, fine-tuning methods (SFT, LoRA, DPO, distillation), recommendation and ranking.

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
