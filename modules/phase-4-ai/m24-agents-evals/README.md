# M24 — Agents and evals

> Plan: `Senior_Engineer_Interview_Plan.html#m24` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A support agent whose tools call the capstone Saga, plus an eval suite that blocks regressions in CI.

## Added topics (learner, 2026-11-21: AI-powered distributed systems)
- [ ] **A2A (agent-to-agent protocol):** agents discovering and calling each other across services and vendors; A2A vs. MCP (agent ↔ tools); handoffs, delegation, trust between agents.
- [ ] **Durable agent execution:** checkpoint every step and resume after crashes / redeploys (Temporal, Durable Functions, LangGraph checkpointers); human-approval waits of hours or days; Sagas with compensation for multi-step agent actions.

## Learning plan

### Big picture (read first)
An **agent** is an LLM in a loop: it decides which tool to call next, sees the result, and repeats until done. That flexibility is powerful and risky (loops, wrong actions, cost). Sessions 1–7 cover when to use agents and how to make them safe, debuggable and durable; sessions 8–10 cover **evals**: how you prove an agent or RAG system works and keeps working. The exit deliverable goes into the end-of-Phase-4 code-only build (backlog in the M25 README).

### Sessions (in order)
1. **Agent vs. workflow:** when an agent is justified.
2. **Tool design:** least privilege, stop conditions, step and cost budgets, human approval.
3. **Debugging agents:** loops and wrong tool calls, from traces.
4. **MCP basics:** exposing tools and resources to agents.
5. **Multi-agent patterns:** orchestrator–worker, evaluator–optimizer; memory; context engineering.
6. **A2A vs. MCP:** agents calling agents; handoffs, delegation, trust.
7. **Durable agent execution:** checkpoint/resume, long approval waits, Sagas with compensation.
8. **Eval sets:** from production failures and edge cases; offline vs. online.
9. **Graders:** exact/state checks, LLM-as-judge with a rubric, human review; judge calibration and bias.
10. **Running evals:** repeated trials, CI regression gates.

### Done when you can answer
- When would you build a fixed workflow instead of an agent?
- How do you stop an agent from looping forever or doing something dangerous?
- MCP vs. A2A: what does each connect?
- An agent crashes halfway through a 5-step task that already charged a card. What happens?
- How do you know a prompt change didn't make the agent worse?

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

#### Agent vs. workflow
- **In plain words:** Workflow = steps I write in code in a fixed order (an LLM may do some steps; my code decides what's next). Agent = a loop where the LLM decides what to do next (which tool, how often, when it's done).
- **Problem it solves:** Using an agent for a known process (summarise → classify → route a ticket) adds skipped/looped steps, variable cost and hard debugging for no gain.
- **Analogy:** Workflow = a recipe (maybe using a food processor); agent = a chef told "make dinner with what's in the fridge".
- **Diagram:** single LLM call → workflow (chain, router, parallel, capped evaluator loop) → agent (LLM picks tools in a loop). Decide: can I write the steps? → workflow; one decision point → workflow + LLM router; path depends on what the model finds → agent; costly actions → agent only with tight tools + approval, or a workflow.
- **Variations / alternatives:** Chain, router, parallel calls, evaluator loop with a cap. Default: start with a workflow; agent only where the path can't be known.
- **When not to use it (agent):** Known repeatable processes, strict latency/cost limits, irreversible actions without approval, nothing to evaluate it with.
- **Quick-check answers:** Nightly sales summary email → workflow (fixed steps). "Help with anything about their account" → **agent** (steps not fixed; say the word that matches the reason). `OrderChatController` is a small agent; risks: wrong tool / wrong id, loops, unintended actions, variable cost; already limited by `MaximumIterationsPerRequest = 5`, owner checks in every tool, cancel = request + user confirmation, no `UpdateOrder` tool + "say you can't".

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
