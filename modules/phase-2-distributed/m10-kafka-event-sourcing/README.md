# M10 — Kafka, stream processing, event sourcing, and CQRS

> Plan: `Senior_Engineer_Interview_Plan.html#m10` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A Kafka order stream with an event-sourced aggregate, a rebuildable read model, and a RabbitMQ vs. Kafka comparison.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
Kafka isn't a queue. It's a **durable, ordered log** that many consumers read at their own pace and can **replay**. Two ideas are built on top of it: **event sourcing** (store every change, not just the current state) and **CQRS** (separate models for writes and reads).

### Sessions (in order)
1. **Log vs. queue:** why Kafka keeps messages; topics, partitions, offsets, retention.
2. **Partitioning and ordering:** the partition key gives per-key order; hot partitions. 🧪 Kafka in Docker with the Confluent .NET client.
3. **Consumer groups and rebalancing:** scaling, partition assignment, pauses.
4. **Delivery semantics:** committing before vs. after processing gives at-most-once vs. at-least-once; idempotent producer; transactions and what "exactly-once" really covers. 🧪 Crash before and after the commit.
5. **Schemas and versioning:** schema registry, backward and forward compatibility, v1 and v2 side by side.
6. **Stream-processing concepts:** windows, late events, watermarks.
7. **Event sourcing:** event store, rebuilding state, snapshots, projections. 🧪 An event-sourced Order aggregate.
8. **CQRS:** separate read models, eventual consistency, when it's overkill.
9. **RabbitMQ vs. Kafka vs. Service Bus:** a decision table.

### Done when you can answer
- How does Kafka keep events in order, and what breaks that order?
- Committing the offset before vs. after processing: which guarantee does each give?
- What does Kafka's exactly-once cover, and what doesn't it cover?
- When is event sourcing worth it, and when is it a trap?

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
