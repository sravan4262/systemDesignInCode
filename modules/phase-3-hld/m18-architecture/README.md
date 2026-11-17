# M18 — Architecture and domain design

> Plan: `Senior_Engineer_Interview_Plan.html#m18` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] An RFC decomposing the capstone into bounded contexts with ADRs, plus a strangler-fig migration plan.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
How to carve a system into the **right pieces**, and how to change it safely over time.
- **DDD** gives you the boundaries: bounded contexts and aggregates.
- **Architecture styles** give each service its internal structure.
- **Migration patterns** let you change a live system without a big-bang rewrite.

### Sessions (in order)
1. **Strategic DDD:** ubiquitous language, bounded contexts, context maps, anti-corruption layer.
2. **Aggregates:** consistency boundaries, one aggregate per transaction, referencing other aggregates by ID.
3. **Inside a service:** layered vs. clean/hexagonal vs. vertical slice, in .NET.
4. **Monolith vs. modular monolith vs. microservices;** the smells of a distributed monolith.
5. **Communication:** sync vs. async, API gateway (APIM), BFF, service discovery, service mesh, retrying in only one place.
6. **Migrations:** strangler fig, branch by abstraction, parallel run, dual writes with backfill, CDC, expand/contract. Practise retelling your own migrations in this vocabulary.
7. **Design docs:** RFCs and ADRs. 🧪 Write an RFC and 2 ADRs that split the capstone into services.

### Done when you can answer
- How do you find service boundaries?
- What is a distributed monolith, and how do you spot one?
- How do you migrate a monolith without a big-bang rewrite?
- Service discovery vs. API gateway: what's the difference?

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
