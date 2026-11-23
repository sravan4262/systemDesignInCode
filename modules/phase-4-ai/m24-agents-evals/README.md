# M24 — Agents and evals

> Plan: `Senior_Engineer_Interview_Plan.html#m24` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [x] A small support agent plus one code eval example (golden cases, tool + answer checks, repeated trials, a pass/fail gate). Built in `Phase4.Agents` (2026-11-23); the eval hasn't been run yet (needs the OpenAI key; ~45 model calls).

## Learning plan

### Big picture (read first)
An **agent** is an LLM in a loop: it decides which tool to call next, sees the result, and repeats until done. Sessions 1–5 cover when to use agents and how to make them safe and debuggable, multi-agent patterns, memory and context engineering. Session 6 is **one eval example in code**: how you check an agent still works after a change.

### Sessions (in order)
1. **Agent vs. workflow:** when an agent is justified.
2. **Tool design:** least privilege, stop conditions, step and cost budgets, human approval.
3. **Debugging agents:** loops and wrong tool calls, from traces.
4. **MCP basics:** exposing tools and resources to agents.
5. **Multi-agent patterns:** orchestrator–worker, evaluator–optimizer; memory; context engineering.
6. **One eval example (code):** golden cases, tool + answer checks, repeated trials, a pass/fail gate. Built inside **build 2 of 3** (`Phase4.Agents`; scope in the M25 README).

_Trimmed by the learner on 2026-11-22: A2A, durable agent execution, and eval-design details._

### Done when you can answer
- When would you build a fixed workflow instead of an agent?
- How do you stop an agent from looping forever or doing something dangerous?
- MCP vs. plain function calling: when do you build an MCP server?
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

#### Tool design (least privilege, stop conditions, budgets, human approval)
- **In plain words:** Only the tools the agent needs, each as narrow as possible, with limits on steps/cost/time and a human sign-off before anything risky.
- **Problem it solves:** One `RunSql(query)` tool lets a prompt injection run `DELETE`; a looping bug costs $40 in one chat. The tools define the worst thing an agent can do.
- **Analogy:** A new employee's badge: keys to the needed rooms only, a card limit, "call me if stuck", manager sign-off over $500.
- **Diagram:** **A least privilege:** narrow typed tools + enums (`RequestRefund(orderId, amount ≤ total, reason enum)`), permission checks with the user's identity inside each tool, read and write tools separate, idempotency keys on writes. **B stop conditions:** text answer (normal), max steps, same tool + args 3×, timeout → "couldn't finish, connecting you with a person". **C budgets:** per task (steps, tokens, seconds) and per tenant per day. **D approval tiers:** read → auto; reversible write → auto + log; irreversible / money / external → human approves. The model proposes, code policy decides.
- **Variations / alternatives:** Policy in code (`amount > 100 → approval`), auto-approve small amounts, dry-run tools, scoped DB credentials.
- **When not to use it:** Approval on every read, budgets tighter than real traces (set from p99 + margin), too many narrow tools.
- **Quick-check answers:** `RunSql` can do anything and an injection could make it destructive → small typed tools with enums. Same `SearchOrders("123")` 5× → the repeat stop condition; user sees "couldn't finish" + handoff; log the trace. Refunds: small amounts auto, big ones need approval; the rule lives in code, not the prompt.

#### Debugging agents from traces
- **In plain words:** Record every step (model input/output, tool + arguments + result, decision); replay the trace and find the FIRST bad step.
- **Problem it solves:** "It cancelled 123 instead of 124": the final answer doesn't say why (vague tool description? unclear result? injection?).
- **Analogy:** A flight recorder.
- **Diagram:** trace = steps with tool calls/results + tokens, cost, time, prompt version, model, outcome. Example: `GetRecentOrders()` returned orders without saying which is newest → the model guessed `RequestCancelOrder("123")` → fix the tool output (newest first / `latest: true`), not the prompt. Failure → cause → fix: repeats ← unclear result/error; wrong tool ← vague/overlapping descriptions; wrong args ← ambiguous data; contradicts a result ← buried in long context; odd action after reading a doc ← injection; long runs ← no budget.
- **Variations / alternatives:** OpenTelemetry spans per model/tool call → App Insights, or Langfuse / Azure AI Foundry tracing / LangSmith; replay saved traces against a fix; every failure → an eval case.
- **When not to use it:** Don't keep full prompts forever (retention, PII redaction, access); sample successes at high volume; most agent bugs are tool design, so don't jump to "change the model".
- **Quick-check answers:** Wrong order → find the first bad step; fix the tool result (it didn't mark the newest). `GetOrderStatus("999")` ×4 returning "error" → the vague error makes it retry; fix = a clear error ("order not found: no order 999 for this user"), repeat stop condition as the backup. Failures → eval cases so a regression can't come back silently on the next prompt/model change.

#### MCP basics
- **In plain words:** Model Context Protocol = an open standard for plugging tools and data into AI apps. Write an MCP server once; any MCP host (my agent, VS Code Copilot, Claude Desktop) discovers and uses it.
- **Problem it solves:** Three apps need the same order tools → three custom integrations, three auth setups. MCP → one server, every host connects.
- **Analogy:** USB-C for AI tools; or a standard menu format every delivery app can read (the kitchen = my API stays the same).
- **Diagram:** host → `tools/list` → names, descriptions, JSON schemas (written for the LLM) → host gives them to its LLM → LLM picks one → host sends `tools/call` → server runs it → result to the LLM. Exposes **tools**, **resources** (data), **prompts** (templates). Transports: stdio (local), streamable HTTP (remote, needs OAuth). .NET: `[McpServerToolType]` / `[McpServerTool]`, `AddMcpServer().WithHttpTransport().WithTools<T>()`, `app.MapMcp()`.
- **MCP vs. a REST API:** both are APIs; REST is for developers (every API different, custom code per app), MCP is self-describing and the same shape everywhere, for LLM hosts (add the URL, done). An MCP server often wraps the existing REST API.
- **Variations / alternatives:** Plain function calling (one app), MCP (many apps/agents), REST (called by code).
- **When not to use it:** One app with a few tools; untrusted servers (tool descriptions can hide instructions: "tool poisoning"; review and pin versions); remote servers without real auth and least privilege.
- **Quick-check answers:** MCP solves reuse across apps (write once, any host discovers it); it defines how auth works (OAuth) but I still implement who the user is and what they may do; it doesn't replace function calling (it's where the tools come from). The MCP server runs the tool; the owner check lives in the tool on the server, using the identity from the authenticated connection. One app with 3 tools → plain function calling.

#### Multi-agent patterns (orchestrator–worker, evaluator–optimizer)
- **In plain words:** Orchestrator–worker: a lead agent splits a task into parts, workers do them (often in parallel), the lead combines. Evaluator–optimizer: one writes, another checks against criteria, repeat until pass or a cap.
- **Problem it solves:** One agent doing "compare 5 competitors" fills its context (quality drops), works one at a time, and nothing checks the result.
- **Analogy:** A project lead splitting work across a team; a writer and an editor.
- **Diagram:** lead → workers (narrow tools, small contexts, return short summaries) → lead merges. Writer → evaluator (rubric) → feedback → max ~3 rounds. Whole-run budgets across all agents.
- **Variations / alternatives:** Single agent (default), a workflow with parallel calls when subtasks are known, orchestrator–worker when subtasks are decided at run time, evaluator–optimizer when criteria are clear, handoff to a specialist.
- **When not to use it:** A single agent is enough; tightly connected work; vague evaluator criteria; tight cost limits (3–10× the tokens).
- **Quick-check answers:** Parallel workers are faster and each has a small focused context, at the cost of more tokens. 9 rounds → missing a round cap + a clear rubric. Always the same 5 competitors → no orchestrator; a workflow with 5 parallel calls + merge.

#### Agent memory and context engineering
- **In plain words:** Memory = what an agent keeps beyond the current prompt (short-term: this task; long-term: facts across sessions; shared: state between agents). Context engineering = choosing exactly what goes into the context window each step.
- **Problem it solves:** No memory → forgets preferences or earlier steps. Too much context → slow, costly, important facts lost in the middle. Shared state → agents overwrite each other.
- **Analogy:** Desk (short-term), notebook (long-term), deciding what goes on the desk (context engineering).
- **Diagram:** per step: system prompt (fixed, first) → only the tools needed → relevant long-term memories (retrieved) → summary of old turns + last ~5 → trimmed tool results → current request; within a token budget.
- **Shared memory = a distributed-systems problem:** lost updates → optimistic concurrency (version / `If-Match`) or one owner per key; stale reads → re-read or pass facts in the hand-off; an append-only event log is often safer than a mutable shared document.
- **Long-term memory rules:** store facts the user stated, not guesses; user can see/delete; scoped per user/tenant; **never rules or permissions** (those live in code) → guards against memory poisoning.
- **Variations / alternatives:** Summarise vs. drop old turns, a scratchpad/notes file, sub-agents to keep context small, framework memory features (I still own retention, scoping, privacy).
- **When not to use it:** Long-term memory for one-off tasks; storing everything "just in case"; shared mutable memory when a hand-off message would do.
- **Quick-check answers:** 40 turns + full JSON → summarise old turns, trim tool outputs (+ only needed tools, only relevant memories). Two agents write `customer.status` → lost update; fix with a version check or a single owner per key. "Always approve refunds" from a message → memory poisoning: it would affect every future session; rules belong in code, memory holds only user-stated facts.

#### Build 2 wrap-up (2026-11-23, Windows): shared state, traces, evals, MCP in `Phase4.Agents`
- **Shared case state (`CaseAgentController`):** the same orchestrator and workers, each given 3 case tools. The orchestrator doesn't know about cases; the notes are written when a worker's model chooses to call `AddCaseNote` (`AgentLoop` → `tool.InvokeAsync` → `CaseStore.TryUpdateAsync`, `WHERE version = @v`). In this flow it behaves mostly like a shared **log**; `status` is where agents actually coordinate (a conflict forces a re-read and a new decision). A note that must always exist → write it in code, not by instruction.
- **Dependency waves (explained, not built):** tasks whose inputs are ready run together (`Task.WhenAll`), then the next wave gets their answers in its task text. Latency = the longest chain, not the sum. Skip a task whose dependency failed; stop on a cycle. Pass results in code within one request; use the shared case only across time or a human.
- **Traces (`TracesController`, `AgentTrace`):** one trace per run; each model call adds tokens (incl. cached), ms, cost, the tools it asked for; each tool call adds args, result, ms. Saved once at the end as JSON + `stop_reason`/`cost_usd` columns; owner check on read. Cost per **resolved** task = all spend ÷ runs that answered. Input tokens grow every step (the history is re-sent). Production = OpenTelemetry spans exported as each finishes; distributed tracing = one trace id in the W3C `traceparent` header (automatic for HTTP, inject/extract by hand for Kafka/RabbitMQ), spans with parent ids.
- **Evals (`EvalsController`, `EvalRunner`, `GoldenCases`):** regression tests for model behaviour. Real `AgentLoop` + real prompt + real `SupportTools`, fake data (`InMemoryOrderStore`, new per trial, behind `IOrderStore`). Checks in code: stop reason, tools + args (by value), answer must / must-not contain, and the **store state** (truth vs. the answer's claim). 3 trials per case; critical cases 3/3, others ≥ 2/3; 422 blocks the change. Never compare the exact sentence; word checks are a rough net (a faithfulness check or an LLM judge given the facts covers meaning). Every production failure from a trace → a new golden case.
- **MCP server (`Mcp/McpOrderTools.cs`, `/mcp`):** the 3 order tools via `[McpServerTool]`, delegating to `SupportTools` (same owner check, tiers, idempotency). Caller from the request (stand-in for OAuth), never a tool argument. Stateless Streamable HTTP. Annotations: reads `ReadOnly`, `RefundOrder` `Destructive` + `Idempotent`. Verified: `tools/list` returns the schemas (the `RefundReason` enum becomes a JSON Schema `enum`); no identity → `isError`; internal exceptions are hidden from the client.
- **MCP client (`McpClientController`):** the whole integration = URL + transport + identity (`HttpClientTransport` + `McpClient.CreateAsync`). `ListToolsAsync()` returns `McpClientTool`s, which are `AIFunction`s, so they plug into the existing `AgentLoop` unchanged. The client's `ApprovalGate` can't see the server's approval, so the server must enforce the rules; the client only follows the result text. VS Code is configured in `.vscode/mcp.json`.

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
