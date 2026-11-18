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

#### Outbox (condensed track, Session 4, 2026-11-17)
- **In plain words:** write the event to an Outbox table in the SAME DB transaction as the business change; a relay publishes it later.
- **Problem:** dual write. Commit-then-publish loses events; publish-then-commit (or publish inside the tx) creates ghost events, because the broker doesn't roll back with the DB.
- **Relay:** polling (`UPDLOCK, READPAST` to let several relays split rows; loses global order) or CDC (Debezium → Kafka). One active relay or partition-by-key when order matters.
- **Guarantee:** never lost, never a ghost, but at least once (crash after publish, before `SentAt` → republished). `MessageId` is set once in the outbox row.
- **Code:** `src/M11.Messaging/Outbox.cs`.
- **Quick-check answers:** Q1 ✅ ghost event (consumers ship/email for a rolled-back order). Q2 ✅ republished, consumer dedups via inbox. Q3 problem ✅ (duplicates); fix ❌ READ UNCOMMITTED: takes no locks AND can publish rows from a tx that rolls back. Correct: UPDLOCK + READPAST, single active relay, or partition by key; also note ordering breaks.

#### Inbox (condensed track, Session 5)
- **In plain words:** record each processed `MessageId` (PK) in the SAME transaction as the business change; a duplicate hits the PK → skip and ACK.
- **Key point:** the constraint is the referee, not `IF EXISTS` (check-then-act race). EF `Add()` doesn't touch the DB until `SaveChanges`, so moving it first fixes nothing.
- **External side effects** (Stripe) can't join the tx: call with a STABLE idempotency key (`order-{OrderId}`); a redelivery gets the original charge back.
- **Outbox + inbox = effectively once** (delivery is still at least once).
- **Code:** `src/M11.Messaging/Inbox.cs`.
- **Q&A (5 questions, ~6.5/10):** Q1 ✅ outbox. Q2 ⚠️ no "pending" status needed when inbox + payment commit together; PK violation → skip → ACK (forgot the ACK). Q3 ❌ moving `Add` first doesn't help; unique constraint + one tx + catch the violation. Q4 ✅ new GUID per publish defeats dedup. Q5 ⚠️ idempotency key ✅, but a failed commit is transient → retry, not DLQ; just call Stripe again with the same key.

#### Failure catalog (condensed track, Session 6)
- Loss (confirms / `acks=all`, outbox, manual ack after processing), duplicates (inbox), poison (classify; DLQ; in Kafka commit past it), retry storm (backoff + jitter, delay queues / retry topics, circuit breaker, cap attempts), ordering broken by retries (versions + park early events, or block the partition), slow consumer / rebalance loop (`max.poll.*`, prefetch), schema changes (additive only, tolerant reader, new event version, consumers deploy first), backlog (lag / oldest-message age alerts).
- Quick check skipped: see open questions.

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
