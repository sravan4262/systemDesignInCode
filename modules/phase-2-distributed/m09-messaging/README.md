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

#### Messaging refresher (condensed track, Session 1, 2026-11-17)
- **Queue vs. pub/sub:** queue = one consumer per message; pub/sub = every subscriber group gets a copy. Real systems use both: one queue per service (pub/sub across services), many instances on that queue (competing consumers). Commands → point-to-point; events → pub/sub.
- **Exchanges:** fanout = every bound queue gets a copy; topic = only queues with a matching binding pattern (`*` one word, `#` zero or more); direct = exact key. Unmatched messages are dropped silently (use `mandatory` or an alternate exchange).
- **Competing consumers:** instances of one service must share the SAME queue name; an auto-named queue per instance turns it into pub/sub (every instance processes every message). The queue gives each message to one consumer at a time, not "once ever": crash before ack → redelivered to another instance.
- **Acks and guarantees:** ack after processing = at least once (duplicates); ack before = at most once (loss). "Exactly once" = at least once + idempotent consumer. Producer side: publisher confirms + durable/quorum queues.
- **Ordering:** broken by competing consumers and requeue; use per-key ordering or versioned, order-tolerant handlers.
- **DLQ:** permanent errors (bad payload) → DLQ immediately; transient → retry with backoff, then DLQ. `requeue: true` on a poison message = hot loop.
- **Quick-check answers:** Q1 ✅ two queues on a fanout exchange (one per service; instances share it). Q3 ✅ malformed → DLQ. Q2 (crash after commit before ack) skipped: answer = redelivered, prevented by inbox + payment idempotency key.

#### RabbitMQ vs. Kafka (condensed track, Session 3)
- **My words:** Kafka keeps the history (it doesn't delete on consume), so we can replay; it's faster because the broker doesn't track an ack per message.
- **Correction:** say "log", not "queue". Speed comes from sequential appends, batching, partitions, zero-copy, and one offset per partition instead of per-message state. Also: Kafka keeps ORDER per key while scaling.
- **One line:** RabbitMQ is a queue (deliver and forget, rich routing, per-message retry/DLQ/delay); Kafka is a partitioned log (retain, replay, per-key order, huge throughput; a poison message blocks its partition).
- **When:** tasks/commands (SendEmail, GenerateInvoicePdf, delayed retries) → RabbitMQ. Events many services read now and later, replay, CDC, audit/clickstream → Kafka. Common hybrid: events on Kafka, task queues on RabbitMQ.

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
