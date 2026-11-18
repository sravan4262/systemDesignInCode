# M12 — Multiple databases, distributed transactions, and Saga

> Plan: `Senior_Engineer_Interview_Plan.html#m12` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A multi-service order Saga that recovers from partial completion and failed compensation, built by hand and on a saga framework.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
One business action, such as placing an order, touches **several services, each with its own database**. There's no single transaction across them. You either **coordinate one commit** (2PC) or **chain local transactions with undo steps** (Saga). This module covers both, and why Saga wins in microservices.

### Sessions (in order)
1. **Local vs. distributed transactions:** a partial-commit demo. 🧪 DB A commits, DB B fails.
2. **2PC:** prepare and commit, the coordinator, in-doubt outcomes, blocking; the limits of `TransactionScope`.
3. **The Saga idea:** local transactions plus compensations; semantic undo (refund, not delete).
4. **Orchestration:** a coordinator with persisted state. 🧪 Build the order saga (reserve → pay → confirm).
5. **Choreography:** services reacting to each other's events; coupling and visibility trade-offs.
6. **Designing compensations:** idempotent, retryable, ordered; things you can't undo (a sent email).
7. **Failure and recovery:** the orchestrator crashes mid-flow, timeouts, stuck sagas. 🧪
8. **NServiceBus sagas and MassTransit state machines:** correlation, timeouts, concurrency on saga state.

### Done when you can answer
- Why not use 2PC across microservices?
- Orchestration vs. choreography: when do you use each?
- The payment succeeded but the inventory reservation failed. Walk through the saga.
- What makes a compensation safe to retry?

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

#### Saga (condensed track, Session 7, 2026-11-17)
- **In plain words:** a business transaction across services = a chain of local transactions; on failure, earlier steps are undone by compensating actions (refund, cancel), not a rollback.
- **Choreography vs. orchestration:** events and reactions (loose, hard to see the whole flow; good for 2–3 steps) vs. a central orchestrator with stored saga state sending commands (visible, handles branching/timeouts; risk of a god service).
- **My words (orchestration):** the orchestrator maintains state shared across commands and knows whether each command succeeded or failed. Missing: WHEN to pick it (4+ steps, branching, timeouts, complex compensation, "where is order 42?").
- **Saga vs. 2PC:** 2PC holds locks across participants and blocks if the coordinator dies; most brokers/cloud DBs don't support it. Saga = eventual consistency, no isolation (use semantic locks like `Pending`).
- **Depth:** compensation ≠ rollback (emails can't be unsent → irreversible steps last); pivot step → after it, retry forward; compensations idempotent (inbox + idempotency key); timeouts = unknown outcome.
- **Transport:** broker-agnostic (RabbitMQ, Kafka keyed by SagaId, Service Bus, or HTTP via Temporal/Durable Functions).
- **Quick check:** Q1 (why not 2PC) and Q3 (double refund) skipped: see open questions.

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
