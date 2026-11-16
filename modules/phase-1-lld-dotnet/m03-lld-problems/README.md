# M3 — LLD interview problems

> Plan: `Senior_Engineer_Interview_Plan.html#m3` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Class diagrams, API contracts, tests, and a 45–60 minute verbal walkthrough for each problem.

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

#### Parking Lot (design done; tests + verbal walkthrough pending)
- **Requirements:** public lot, configurable floors/slots; vehicle types Motorcycle/Car/Truck, slot sizes Small/Medium/Large, a vehicle fits its size or bigger; system assigns the nearest slot (lowest floor, then lowest number); hourly fee rounded up, rate by vehicle type; pay cash/card at exit; lost ticket = flat daily max; lot full → refuse entry. Non-functional: no double assignment across concurrent gates; extensible pricing/allocation. Out of scope: reservations, passes, EV/handicapped, payment internals.
- **Entities:** ParkingLot, Floor, Slot (source of truth for "is it free"), Ticket (SlotId, plate, vehicle type, EntryTime, ExitTime, Status Active→Paid→Exited, Payments list), Payment (attempts: Failed/Succeeded), Gate.
- **Services:** `ISlotAllocator` (Strategy; nearest-first with one min-heap per SlotType + lock), `IPricingStrategy` (Strategy; rates are data), `ITicketService` (ConcurrentDictionary by ticket ID), `IPaymentProcessor` + factory (cash/card), `ParkingService` coordinator (`Enter`, `Exit`); gates only call the coordinator.
- **Flows:** Enter = Allocate (null → full) → GenerateTicket (throws → Release, compensation). Exit = Find → CalculateFee → Pay (failed → stop, slot stays taken) → Release → Exited.
- **Concurrency:** check-then-act race; `lock` around allocation only (small critical section); across servers → atomic `UPDATE ... WHERE IsEmpty = 1`, check rows affected.
- **Lessons:** signatures = inputs the caller knows, outputs the service decides (never pass `isEmpty`, `status`, new ids). In LLD rounds use in-memory structures behind an interface; mention the DB, don't build it. Discussion rounds: code only the core 30–50 lines.
- **Code:** `src/M03.ParkingLot/ParkingLotDesign.cs`

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
