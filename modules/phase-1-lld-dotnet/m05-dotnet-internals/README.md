# M5 — .NET platform internals

> Plan: `Senior_Engineer_Interview_Plan.html#m5` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Fix a seeded ThreadPool-starvation bug and a seeded N+1 bug with dotnet-counters/dotnet-trace; explain the Kestrel → middleware → DI scope → EF Core flow.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### Hot topics covered (concept only, 2026-11-16)
- **async/await:** compiler state machine; frees the thread during I/O (throughput, not speed). `.Result`/`.Wait()` deadlocks with a SynchronizationContext (WinForms, classic ASP.NET); in ASP.NET Core it causes thread-pool starvation. Async all the way; `WhenAll` for parallel I/O; `ConfigureAwait(false)` in libraries; `async void` only for event handlers.
- **DI lifetimes:** singleton (one per app, thread-safe), scoped (one per request), transient (per injection). Captive dependency = longer-lived service holds a shorter-lived one; scope validation catches it (Dev by default). Background services: `IServiceScopeFactory` / `IDbContextFactory`.
- **Middleware:** ordered chain, in on the way in and reverse on the way out, can short-circuit. ExceptionHandler first; Routing → AuthN → AuthZ; CORS before auth. Filters = MVC-only, know the action.
- **IHttpClientFactory:** new client per call → TIME_WAIT socket exhaustion; static client → stale DNS. Factory pools handlers and recycles them (2 min). Typed clients; don't capture in a singleton; alternative `SocketsHttpHandler.PooledConnectionLifetime`.
- **N+1:** list query + one query per row (lazy loading, queries in loops). Fix: projection (`Select`), `Include`, batch with `IN`; `AsSplitQuery` for cartesian explosion; detect by logging SQL.
- **DbContext lifetime:** unit of work + change tracker, not thread-safe, scoped per request; one operation at a time; factory for background/parallel; new context or `ChangeTracker.Clear()` per batch; `AddDbContextPool` reuses instances (no per-request state).

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
