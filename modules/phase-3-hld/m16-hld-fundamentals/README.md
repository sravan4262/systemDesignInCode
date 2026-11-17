# M16 — HLD fundamentals and real-time delivery

> Plan: `Senior_Engineer_Interview_Plan.html#m16` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Timed designs with estimates and trade-offs, plus a one-page plan for scaling SignalR to 1M connections.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
The vocabulary and method for system design interviews. This module covers:
- the standard building blocks (DNS, load balancers, gateways, CDN)
- how to **size** a system with back-of-envelope maths
- the CAP trade-off
- a **delivery framework** to use every time
- how servers **push** data to clients in real time

### Sessions (in order)
1. **The delivery framework:** requirements → entities → API → high-level design → deep dives → scale → wrap-up; leading the conversation.
2. **The request path:** what happens when you type a URL (DNS, TCP, TLS, HTTP/1.1, 2 and 3).
3. **Building blocks:** L4 and L7 load balancers, reverse proxy, API gateway, CDN, object storage, stateless scaling.
4. **Back-of-envelope maths:** latency numbers, QPS, storage, bandwidth, server count. 🧪 Build a cheat sheet, then size 3 systems.
5. **Data choices as HLD decisions:** SQL vs. NoSQL, replication, partitioning, caching (a recap of Phase 2).
6. **CAP and PACELC,** consistency models in practice, failure domains.
7. **Real-time delivery:** polling, long polling, SSE and WebSockets, and when each fits.
8. **SignalR at scale:** backplane vs. Azure SignalR Service, reconnect storms, presence, resyncing missed messages; mobile push.

### Done when you can answer
- Estimate QPS and storage for a given product in 2 minutes.
- SSE vs. WebSockets: when do you use each?
- What does CAP actually force you to choose, and when?
- How do you scale WebSocket connections across many servers?

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
