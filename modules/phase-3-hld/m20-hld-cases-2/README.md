# M20 — HLD case studies II

> Plan: `Senior_Engineer_Interview_Plan.html#m20` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Write-ups for each case, including payment exactly-once and hotel reservation.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
More cases. Some you've already designed at LLD level (rate limiter, notification service, hotel), now at **system scale**. The others are correctness-heavy designs such as **payments**. You'll also practise tuning an answer to the company type: fintech, healthcare, social or streaming.

### Sessions (in order)
1. **Rate limiter:** distributed counters, multi-region, fail-open vs. fail-closed.
2. **Distributed key-value store and distributed cache.**
3. **Notification system:** channels, templates, preferences, retries, dedup, priority.
4. **Typeahead and web crawler.**
5. **Ticketmaster:** flash-sale contention, virtual waiting room, fairness.
6. **Payment system:** double-entry ledger, reconciliation; the deep dive on making sure a payment never happens twice.
7. **Hotel and meeting-room reservation** at system scale.
8. **Job scheduler, top-K / ad-click aggregation, leaderboard, metrics and logging platform.**
9. **Google Docs, "design Kafka", auction, stock brokerage.**
10. **Your own question bank:** feature flags, CI/CD on AKS, zero-downtime SQL migration, HL7 ingestion, multi-tenant auth, multi-region DR. Pick about 10–12 by priority. 🧪 Each one timed, with a diagram in `designs/`, then reviewed.

### Done when you can answer
- Does every design cover requirements, API, data model, estimates, architecture, bottlenecks, failure modes, security and cost?
- How do you guarantee a payment is never charged twice, end to end?
- Should a rate limiter fail open or fail closed when Redis is down? Why?
- How does your answer change for a fintech vs. a social media company?

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
