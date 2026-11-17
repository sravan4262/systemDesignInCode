# M9 — Messaging and queue reliability

> Plan: `Senior_Engineer_Interview_Plan.html#m9` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A duplicate-safe order-event pipeline built on raw RabbitMQ and on NServiceBus or MassTransit, with each guarantee explained.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
Services talk by leaving **messages in a broker** instead of calling each other directly. That decouples them and absorbs traffic spikes, but brings new failures: messages that are **lost, duplicated, reordered or stuck**. This module covers what RabbitMQ guarantees, and exactly where each guarantee ends.

### Sessions (in order)
1. **Why messaging:** sync vs. async, decoupling, load levelling; queues vs. pub/sub.
2. **The RabbitMQ model:** exchanges (direct, topic, fanout), bindings, routing keys, queues. 🧪 RabbitMQ in Docker with a .NET producer and consumer.
3. **Competing consumers and prefetch:** scaling workers, fair dispatch.
4. **Acknowledgements:** publisher confirms vs. consumer acks; durable queues and persistent messages. 🧪 Crash a consumer before and after its ack.
5. **Failure catalogue:** broker outage, lost confirm, a crash after the DB commit but before the ack → duplicates → idempotency.
6. **Ordering and poison messages:** per-aggregate ordering, head-of-line blocking, requeue loops, dead-letter exchanges.
7. **NServiceBus and MassTransit:** what the frameworks add (recoverability, error queue, monitoring).
8. **Beyond RabbitMQ:** Service Bus (locks, sessions, DLQ), a first look at Kafka, claim-check, quorum queues.

### Done when you can answer
- Publisher confirm vs. consumer ack: what does each one guarantee?
- A consumer commits to the database, then crashes before acking. What happens, and how do you make it safe?
- How do you keep one order's messages in order while still scaling consumers?
- What is a poison message, and how do you stop it blocking the queue?

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
