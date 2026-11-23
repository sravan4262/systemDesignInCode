# Hybrid router: customer-support assistant (M25 system design)

Diagram: [hybrid-router.excalidraw](hybrid-router.excalidraw), in two rows:
- **A. Customer:** Customer → APIM → Conversation Service → guardrails → **Router**, which then splits into three lanes: a **fixed flow** for actions (top), **AI + RAG** for questions (middle), and **hand-off** to a human (bottom).
- **B. Human support agent:** Agent → APIM → Agent console. **Write path** (top): claim, reply, approve. **Read path** (bottom): queue, summary, history.

Purple boxes are AI model calls. To view or edit the diagram, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

**How this case differs from "AI chat over company documents":** that one only **answers**. This one also **acts** (refunds, cancellations), and its core problem is **deciding who handles each message: fixed code, AI, or a human**.

## Prompt (what the interviewer says)
> "Design a customer-support assistant for an online shop. Customers chat about their orders, refunds and general questions. Some conversations need a human."

## Functional requirements (in scope)
1. **Chat** in the web and mobile app for **logged-in customers**. Replies stream back.
2. **Transactional requests**: order status, cancel an order, refund, change the delivery address. They run as **fixed flows**:
   - deterministic steps;
   - typed calls to the real Order and Refund APIs;
   - a **confirmation** step before any change;
   - **approval by a human** for refunds above a limit.
3. **Open questions** (policies, shipping, products) are answered by **AI from help-centre articles**, with citations. If nothing relevant is found, the assistant says so and offers a human.
4. **Hand-off to a human** happens when:
   - the customer asks for one;
   - the router isn't confident;
   - the topic is sensitive (complaint, legal, fraud);
   - the bot has failed twice;
   - the value is high.

   The agent gets a **summary + the history**, so the customer doesn't have to repeat themselves. The customer sees their place in the queue.
5. **Agent console**:
   - a queue of hand-offs, ordered by priority and waiting time;
   - claim a conversation;
   - reply in real time;
   - approve or deny pending refunds;
   - hand the conversation back to the bot.

**Out of scope (say this out loud):** voice and phone, email tickets, other languages, outbound messages, agent rota and staffing tools, CSAT surveys, training models.

## Non-functional requirements
| Requirement | Target | Why |
|---|---|---|
| First visible reply | < 2 s (TTFT for AI answers; fixed flows are faster) | It's a chat |
| Correct actions | **Never** act on another customer's order; a refund happens **once** | Money; "exactly once" here = at-least-once delivery + an idempotency key |
| Routing accuracy | ≥ 95% on the eval set; **actions never routed to free-form AI** | A wrong route = a wrong action or a useless answer |
| Hand-off wait | p90 < 2 min in business hours | Escalated customers are already unhappy |
| Availability | 99.9% for chat; if the AI is down, fixed flows and hand-off still work | Degrade gracefully, don't go dark |
| Cost | Tokens per conversation ≈ fractions of a cent; **human time is the real cost** | See capacity |

## Capacity estimate (high level)
**Facts:**
- 5M customers; **200k conversations a day**, 6 messages each.
- Peak = 3× average (sales: 10×).
- Message mix: 30% transactional, 50% open questions, 15% greetings and thanks, **5% handed off**.
- A human handles a conversation in ~6 min.
- 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Messages | 200k × 6 | **1.2M/day ≈ 12/s avg, ~40/s peak** | A tiny request rate; a few instances per service |
| Router calls (small model) | 1.2M × ~500 tokens | **~600M tokens/day ≈ $60/day** | Cheap; rules first skip ~15% (greetings) |
| AI answers (RAG) | 50% = 600k × ~3.3k tokens | **~2B tokens/day ≈ $1k/day** (−30% with the semantic cache) | Tokens matter, but they are not the biggest cost |
| Hand-offs | 5% of 200k | **10k/day × 6 min = 1,000 agent-hours/day** | ~125 agents on 8 h shifts (~155 at 80% occupancy) |
| Conversation storage | 1.2M × ~1 KB | **~1.2 GB/day → ~450 GB/year** | Cosmos DB; keep 1 year, then archive |

**What the numbers tell you:**
- **Humans are the expensive part.** About 155 agents cost far more than ~$1k a day of tokens.
- So the biggest win is **resolving more conversations safely without a human**, not shaving tokens. That means good fixed flows for the common actions and good answers for the common questions.
- **Every 1% of conversations that no longer needs a hand-off saves ~30 agents.**

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /conversations` | — | `201 { conversationId }` | Customer from the token |
| `POST /conversations/{id}/messages` | `{ text }` | **SSE stream**: `token` events, or `action_proposal { actionId, summary }`, or `handoff { queuePosition }`; then `done { route, citations }` | The main call |
| `POST /actions/{actionId}/confirm` | — | `200 { result }` or `202 { approvalId }` (needs a human) | `actionId` = **idempotency key**: double-tap → same result |
| `GET /conversations/{id}/messages?before=` | — | `200 { messages[], nextCursor }` | Owner only |
| `GET /agent/queue` | — | `200 { items: [{ conversationId, reason, priority, waitingSec, summary }] }` | Agents only (Entra ID role) |
| `POST /agent/conversations/{id}/claim` | — | `200` or `409` (already claimed) | Conditional update: one agent per conversation |
| `POST /agent/conversations/{id}/messages` | `{ text }` | `202` | Pushed to the customer in real time |
| `POST /agent/approvals/{approvalId}/approve` or `/deny` | `{ note? }` | `200 { result }` | Runs the refund in one transaction (Build 2 `ApproveAsync`) |
| `POST /agent/conversations/{id}/release` | — | `200` | Back to the bot |

Errors: `401` no token, `403` not your conversation or not an agent, `409` already claimed or the action already confirmed (returns the first result), `429` rate limit.

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Technology |
|---|---|---|
| APIM | Customer login (Entra External ID) and agent login (Entra ID + role); rate limits | Azure API Management |
| Conversation Service | **State per conversation:** `Bot → WaitingForHuman → WithHuman → Bot / Closed`; stores the messages; streams to the customer | ASP.NET Core + SignalR / SSE; **Cosmos DB** (pk `conversationId`, plus a by-customer index) |
| Input / output guardrails | PII redaction, moderation, exfiltration guard (Build 3) | Code + Azure AI Language / Content Safety |
| **Router** | ① **rules** (greetings → canned reply; "human/agent" → hand-off; sensitive words → hand-off) → ② **intent classifier** (small model, structured output `{ intent, entities, confidence }`) → ③ dispatch by intent + confidence | Code + a small model |
| **Flow engine** | Fixed flows for transactional intents: fill the slots ("which order?") → **confirm** → call the API → reply. No free-form model decides an action. | Code (a state machine per flow); typed clients |
| Order / Refund APIs | The existing systems. **Owner check with the customer from the token**, idempotency key, refund tiers (≤ 50 auto, more → pending approval) | Existing services + SQL |
| RAG answerer | Open questions: semantic cache (scoped) → hybrid search over help-centre articles → top 5 → answer model → citations, or "I don't know" | Azure AI Search + Azure OpenAI; Redis cache |
| Hand-off service | Writes a **summary** (small model), sets priority, puts the conversation in the queue, tells the customer their position | Service Bus queue + a priority view in SQL |
| Agent console | The queue, claim, live chat, approvals | Web app + SignalR |
| Traces + evals | A trace per conversation (route, confidence, tools, tokens, cost); evals on route accuracy and task success | App Insights / OpenTelemetry; eval suite in CI |

### Path 1: transactional (fixed flow): "Refund order 124, it arrived broken"
1. Customer → APIM (token: customerId) → Conversation Service → **input guardrails**.
2. **Router:** no rule matches → the classifier returns `{ intent: refund, orderId: 124, reason: damaged, confidence: 0.97 }` → **flow engine** (above the confidence threshold, a known intent).
3. **Flow engine:** gets order 124 **with the customer's id** (owner check in the API) → computes the refundable amount → sends `action_proposal` ("Refund 40 to your card?").
4. The customer taps **Confirm** → `POST /actions/{actionId}/confirm` → Refund API with **idempotency key = actionId**:
   - **≤ 50:** refunded → "Done: 40 refunded."
   - **> 50:** a pending approval → "A team member will review this within 24 h" → the approval appears in row B.
- **Why a fixed flow and not an agent:** the steps are known, a model must never decide *how much* to refund, and code is testable and cheaper. The model only **classifies and extracts**; the code **acts**.

### Path 2: open question (AI + RAG): "Can I return opened headphones?"
1. Same entry and guardrails → **router:** `{ intent: policy_question, confidence: 0.9 }` → RAG answerer.
2. **Semantic cache** (scope = tenant + language + help-centre version). A hit returns the answer with no model call (Build 3).
3. Miss → hybrid search over help-centre articles → rerank → top 5 → **answer model** (strict: only from the sources, cite them, "I don't know" otherwise) → **output guardrails** → stream → store in the cache.
4. Nothing relevant (low search score) → "I'm not sure. Want me to connect you to a person?"
- **Not cached:** anything personal ("my order"). Those go to Path 1.

### Path 3: hand-off to a human
**Triggers:** the customer asks; confidence < 0.6; a sensitive intent (complaint, legal, fraud); 2 failed turns in a row; a high-value order; the AI or search is down.

1. The hand-off service creates a **summary** (small model: intent, order, what was tried) and a priority (VIP, value, waiting time).
2. Conversation state → `WaitingForHuman`. **The bot stops answering.** The customer sees "You're number 4, about 3 minutes."
3. Into the queue → the console shows it (row B).

### Row B: the human agent
- **Read:** the queue (priority + waiting time), then the **summary + full history + the customer's orders** (read-only).
- **Write:**
  - **claim** with a conditional update `WHERE state = 'WaitingForHuman'`, so two agents can't take the same customer (`409` for the second);
  - reply, pushed to the customer through SignalR;
  - **approve or deny** pending refunds (one transaction: delete the pending row + refund, Build 2);
  - **release** back to the bot or close.

## Which components each path touches
| Requirement | Path | Components |
|---|---|---|
| Order status / cancel / refund / address | Write (fixed flow) | APIM → Conversation Service → guardrails → **router (small model)** → **flow engine** → confirm → Order / Refund API (owner check, idempotency, tiers) → reply |
| Open question | Read (AI) | APIM → Conversation Service → guardrails → **router** → semantic cache → search (help centre) → **answer model** → output guardrails → stream |
| Hand-off | Write (async) | Router or trigger → hand-off service (**summary model**) → queue → state `WaitingForHuman` |
| Agent works the queue | Read + write | APIM (agent role) → console → queue / history (read) → claim / reply / approve / release (write) |
| Greetings | — | Router rule → canned reply (no model) |

**What the action path never uses:** a free-form model deciding amounts or which order. **What the AI path never uses:** write APIs.

## Deep dives to drive (learner leads, interviewer pushes)
1. **Routing:**
   - rules first, then the classifier;
   - **two confidence thresholds**: act or answer / ask a clarifying question / hand off;
   - how you'd measure the route accuracy and tune the thresholds.
2. **Fixed flow vs. agent for actions:**
   - why the refund is a state machine and not an LLM with a `RefundOrder` tool;
   - when an agent *is* worth it (a long tail of unusual requests, with read-only tools).
3. **Slot filling and clarification:** "refund my order" when the customer has 3 recent orders → list them as buttons, don't guess.
4. **Exactly-once refunds:** the idempotency key from `actionId`; double-taps; retries after a timeout (the outcome is unknown → retry with the same key).
5. **Hand-off quality:**
   - what goes in the summary;
   - avoiding "the bot is still talking while the human is" (the state machine);
   - handing back to the bot.
6. **Degradation:**
   - the model is down → fixed flows still work for buttons and menus, and everything else is handed off;
   - search is down → no AI answers, hand off;
   - the queue is long → "we'll email you", or call back later.
7. **Safety:**
   - injection in a help-centre article can't trigger actions, because only the flow engine acts and only after the customer confirms;
   - the owner check uses the token;
   - PII is redacted before the model and the logs.
8. **Evals and metrics:**
   - **route accuracy**, **task success** per flow, **containment rate** (resolved without a human), **escalation rate**, CSAT after hand-off;
   - a golden set includes tricky routing cases ("I want to complain about my refund" → hand-off, not the refund flow).

## Interview follow-ups (answer these after the design)
- A customer writes: *"refund order 124 and also what's your return policy for laptops?"* How does the router handle two intents in one message?
- The classifier says `refund` with confidence 0.62. What happens, and why not just run the flow?
- Containment drops from 80% to 72% after a model update. What do you check first, and how do you roll back?
- Two agents click "claim" on the same conversation within a second. What happens?
