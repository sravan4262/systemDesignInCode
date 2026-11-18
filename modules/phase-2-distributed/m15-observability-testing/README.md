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

#### Three pillars (Session 1, 2026-11-17)
- **Metrics** = that something is wrong (cheap counters, alerts, trends); **traces** = where (one request across services, spans with timings); **logs** = why (event detail). Trace ID links traces and logs; metrics link by time window or exemplars.
- **Incident flow:** p99 alert → slow trace shows Payments → Stripe 3 s → logs for that trace id show timeout + retry → fix timeout (p99 + margin).
- **Sampling:** keep only a fraction of traces to control cost (spans for every request are expensive); metrics count everything. Head-based (decide at start, whole chain kept/dropped, may drop the failing one) vs. tail-based (decide at end, keep all errors/slow + a % of normal; needs a buffering collector).
- **Quick-check answers:** Q1 ✅ metrics tell that, logs explain why. Q2 ✅ trace id (refined: metrics are aggregates → exemplars/time window). Q3 asked "what does sampled mean?" → explained above.


#### OpenTelemetry tracing (Session 2, 2026-11-17)
- **Propagation (in-band):** only W3C `traceparent` = trace id + CURRENT span id (+ sampled flag) travels; span data never goes in headers. Each service creates its own spans pointing at the parent; nobody updates another service's span. In .NET a span = `Activity`; `Activity.Current` supplies the parent id.
- **HTTP hops automatic** (ASP.NET Core + HttpClient instrumentation read/write traceparent). **Queue hops:** producer INJECTS traceparent into message headers, consumer EXTRACTS it and starts a Consumer span with that parent (MassTransit / broker instrumentation do it for you). Store traceparent in the outbox row too. `ParentBasedSampler` keeps a trace all-or-nothing.
- **Export (out-of-band):** each service batches its finished spans and sends them (OTLP, background) to one central collector/backend; no "end of trace" moment; backend assembles by trace id + parent ids at read time. Clock skew across servers; tail sampling needs all spans of a trace at one collector.
- **App Insights:** separate rows in `requests` (server/consumer spans), `dependencies` (client/SQL/publish), `traces` (= LOGS), `exceptions`; `operation_Id` = trace id, `operation_ParentId` = parent span, `cloud_RoleName` = service. End-to-end view / KQL groups by operation_Id at query time; storage stays individual. All services must send to the same resource/workspace.
- **Code:** `src/M15.Observability/Tracing.cs`.
- **Quick-check answers:** Q1 ✅ orders copies the trace id into the message header, consumer reads it and starts a span (refine: whole traceparent incl. publish span id as parent). Q2 ⚠️ checkout trace stops at publish ✅; Billing's work becomes a SEPARATE unlinked trace (not missing).


#### Structured logs + correlation IDs (Session 3, 2026-11-17, concise)
- Message templates (`"Order {OrderId} placed", id`) store named FIELDS → query/aggregate by field; `$"…"` interpolation = opaque text (and builds the string even when the level is off).
- OTel/App Insights logging stamps every `ILogger` line inside a span with TraceId/SpanId (`operation_Id`); `BeginScope` adds business fields (OrderId) to many lines.
- Rules: meaningful levels; never log secrets/PII; log an exception once where it's handled.
- Quick check skipped.


#### Key metrics (Session 4, 2026-11-17, concise)
- RED for APIs (rate, errors, duration p95/p99), USE for resources (utilization, saturation, errors).
- Phase 2 metrics: consumer lag / queue depth (growing), OLDEST MESSAGE AGE (staleness; better than depth), DLQ size (any increase), outbox backlog + oldest unsent age, pool in-use/wait, deadlocks/s, breaker state, p99 per dependency.
- OTel `Meter` counters/gauges; keep tags LOW-cardinality (no OrderId/UserId: each value = a new time series → those go in logs/traces).
- Quick check skipped.

#### SLOs (Session 5): skipped by choice
- One line kept: alert on p99, not the average, because averages hide the slow tail (1% of users at 5 s vanishes in a 200 ms average).

#### Testing strategy (Session 6, 2026-11-17)
- Pyramid: many unit (pure logic, no I/O, TimeProvider), many integration (real SQL/Redis/RabbitMQ via Testcontainers, WebApplicationFactory), some contract (Pact / schema checks: producer and consumer agree on API/message shape), few E2E (critical journeys only).
- Outbox CI: commit-together + rollback-neither (real SQL), relay publishes + marks SentAt, kill relay between publish and SentAt → republished, two relays → each row once, inbox dedups same MessageId, contract test on OrderPlaced shape.
- **Quick-check answers:** Q1 ⚠️ real DB ✅ but reason off: a mock can't reproduce transaction atomicity/rollback/locking, which IS the outbox guarantee. Q2 ✅ contract tests (self-corrected from E2E): fail in the producer's CI before deploy.


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
