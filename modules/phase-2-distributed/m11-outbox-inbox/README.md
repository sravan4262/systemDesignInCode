# M11 — Outbox, inbox, retries, and DLQ operations

> Plan: `Senior_Engineer_Interview_Plan.html#m11` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] An outbox/inbox pipeline that tolerates duplicate publication and delivery, plus safe DLQ replay and observable backlog.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
The toolkit for "**never lose a message, never apply it twice**".
- The **outbox** fixes the producer side: a DB commit and a broker publish can't be one transaction.
- The **inbox** fixes the consumer side: duplicates.
- **Retries and the DLQ** handle failures without losing messages or looping forever.

This module answers your M2 open question (DB transaction vs. broker publish).

### Sessions (in order)
1. **The dual-write problem:** a DB commit plus a broker publish is two systems with no shared transaction; the four crash points.
2. **Transactional outbox:** the business row and the outbox row in one transaction. 🧪 Build it with EF Core.
3. **The relay:** polling publisher, leases and claims, concurrent relays (`UPDLOCK`/`READPAST` again), status and retries. 🧪 Two relays, with a crash at each step.
4. **Inbox / idempotent consumer:** a processed-message table in the same transaction as the business change. 🧪 Deliver a duplicate.
5. **Retries:** transient vs. permanent errors, exponential backoff with jitter, retry budgets, delayed retries.
6. **DLQ operations:** failure metadata, inspection, selective replay, throttling, quarantine.
7. **CDC vs. polling,** ordering, outbox cleanup.
8. **Framework outboxes:** NServiceBus and MassTransit, and what each one guarantees.

### Done when you can answer
- Why can't you just publish after `SaveChanges`? Walk through the failure.
- The outbox gives at-least-once publishing. What makes the whole flow effectively-once?
- Why must the inbox record be in the same DB transaction, and not in Redis?
- How do you replay 10,000 DLQ messages safely?

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
