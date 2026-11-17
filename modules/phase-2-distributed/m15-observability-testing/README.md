# M15 — Observability, testing strategy, and production operations

> Plan: `Senior_Engineer_Interview_Plan.html#m15` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] An operational dashboard and a failure diagnosed from telemetry rather than guesswork.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
Once a system is distributed, you can't debug it by reading one log file. **Observability** (traces, logs, metrics) lets you follow one request across services and spot trouble **before users do**. A **testing strategy** and **SLOs** stop it from getting worse over time.

### Sessions (in order)
1. **The three pillars:** logs, metrics and traces, and what each one answers.
2. **OpenTelemetry in ASP.NET Core:** traces and context propagation over HTTP and messaging. 🧪 A Jaeger trace across 2 services and a queue.
3. **Structured logs and correlation IDs;** mapping App Insights and AppDynamics concepts onto OpenTelemetry.
4. **Metrics for this phase:** queue depth, oldest message age, consumer lag, DLQ size, outbox backlog, deadlocks, pool usage. 🧪 A Prometheus and Grafana dashboard.
5. **SLIs, SLOs and alerting:** p95 and p99, error budgets, alert fatigue, runbooks.
6. **Incident drill:** a broker outage, a growing backlog, a slow DB, cascading latency.
7. **Testing strategy:** unit, integration (Testcontainers), contract (Pact), end-to-end, property-based, chaos.

### Done when you can answer
- Why alert on p99 latency and not on the average?
- How does a trace follow a request through a message queue?
- Which metric tells you a consumer is falling behind?
- Which tests would you put in CI for an outbox?

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
