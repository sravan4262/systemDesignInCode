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

#### Movie Ticket Booking (design only, no code by choice)
- **Requirements:** multi-city; city → movies → theaters/shows for 7 days (sorted by name, no geo); seat map per show; hold 1–10 seats of one show for 10 min; price per show per seat type (Regular/Premium/Recliner); pay card/wallet; cancel confirmed booking ≥2h before (>24h full, 2–24h 50%). Non-functional: one seat → one user, one booking → one charge, holds expire, extensible payment/pricing/refund.
- **Entities:** City, Theater, Screen, Seat (physical), Movie (catalog), Show (movie + screen + time + prices), **ShowSeat** (seat per show: Status Available/Held/Booked, BookingId, HoldExpiresAt) = source of truth, Booking (Pending/Confirmed/Cancelled/Expired), Payment (attempts, IdempotencyKey).
- **Services:** `ICatalogService` (browse), `ISeatHoldService.Hold(userId, showId, seatIds)` (atomic all-or-nothing claim: `UPDATE ShowSeats ... WHERE Status='Available'`, rows < requested → roll back), `IPaymentProcessor` (Strategy + Factory), `IRefundPolicy` (Strategy), `BookingService` coordinator (`HoldSeats`, `Pay`, `Cancel`), `HoldExpiryJob`.
- **Payments:** one idempotency key per payment attempt, reused on retry; timeout = unknown → retry same key; declined = final → new attempt key; paid after hold expired → auto refund.
- **Lessons:** interviewer's constraints are the spec (no distance sort); put the hard answers into the requirements; status lives on the per-show copy (ShowSeat), not on Seat; pass `showId` + specific seats, never let the caller send the amount.

#### Expense Sharing (design only)
- **Requirements:** expenses with friends or in groups (≤50); split Equal / Exact (sums to total) / Percent (sums to 100), more later; whole cents, shares sum to the exact total (leftover cent → payer); net balance per pair; settle-up recorded in-app (payment outside); pluggable notifications; stretch: simplify debts.
- **Entities:** User, Group, Expense (Total, PaidByUserId, GroupId?, SplitType, Splits), Split (UserId, Amount), Settlement (From, To, Amount, GroupId?), Balance (pair → net amount).
- **Services:** `ISplitStrategy` (Equal/Exact/Percent → `List<Split>`, validates, rounds) via factory; `ExpenseService.AddExpense(paidBy, total, splitType, participants, groupId?)` updates pair balances in the same transaction; `SettlementService.Settle(from, to, amount, groupId?)`; `BalanceService.GetBalances(userId)`; notifier hook (Observer).
- **Lessons:** one-to-many never fits in one field (Splits list); one AddExpense with optional groupId, not two; caller sends split inputs, the strategy computes amounts; a settlement needs from, to, amount; store running balances (fast reads), rebuildable from history.

#### ATM (design only; services skipped)
- **Requirements:** one bank's ATM, any bank's debit card (BIN → bank network, external API); one card per session; balance/withdraw/deposit; 3 wrong PINs → card retained (bank also blocks); cassettes $100/$50/$20, ≤2,000 notes each, only dispensable amounts, reject before debiting; $1,000 per withdrawal, $2,000/day per card (bank enforces); low cash → alert, empty → OutOfService.
- **Entities:** Atm (State, Cassettes), Cassette (Denomination, Count), Card, Session (PinAttempts), AtmTransaction (Type, Status, AuthCode), JournalEntry. Accounts live at the bank, not the ATM.
- **State pattern:** Idle → CardInserted → Authenticated → (transactions) → eject → Idle; 3rd wrong PIN → CardRetained; any → OutOfService.
- **Withdrawal:** Authorize (hold) → Dispense → Confirm; jam → Reverse; power loss mid-dispense → Unknown → reconciliation (journal vs cassette count).
- **Model services (not discussed):** `AtmService` (InsertCard, EnterPin, Withdraw, Deposit, Balance, Eject) delegating to the current `IAtmState`; `ICashDispenser.Plan(amount, cassettes)` (try combinations, not plain greedy: $60 = 3 × $20); `IBankNetwork` (VerifyPin, Authorize, Confirm, Reverse, Balance) with idempotent transaction IDs.
- **Recurring habits to fix (Movie, Expense, ATM):** (1) list requirements before entities; (2) for every field ask "can there be more than one?" (seats, splits, cassettes); (3) model only what the system owns (ATM doesn't own accounts).

#### Hotel Room Booking (design only)
- **Requirements:** one chain, many hotels by city; search city + dates → room types free EVERY night + total; book 1–5 rooms of a type, same hotel/dates, ≤30 nights; price = sum of nightly rates (vary by night); 10-min hold; cancel free ≥48h before check-in, else first night charged (policy per hotel). NFR: never oversell, all nights or none, holds expire, never charge twice, extensible pricing/cancellation.
- **Entities:** Hotel, RoomType (TotalRooms), **RoomInventory** (hotel, type, Date, TotalRooms, Reserved) = source of truth, RoomRate, Booking (CheckIn/CheckOut, Qty, Status, HoldExpiresAt), BookingNight (price snapshot), Payment. `Room` not used (assigned at check-in, out of scope).
- **Atomic claim:** one `UPDATE RoomInventory SET Reserved += qty WHERE ... Date in range AND TotalRooms - Reserved >= qty`; rows affected must equal nights, else ROLLBACK.
- **Services:** `ISearchService`, `IInventoryService.Hold/Release(hotel, type, qty, in, out)` (one call for all nights), `IPricingStrategy`, `ICancellationPolicy`, `BookingService` (Book → Pending, Pay, Cancel), `HoldExpiryJob`.
- **Lessons:** copy interviewer numbers exactly (48h, not 24h); NFR = the promise first, mechanism second; holding and paying are separate user steps; booking problems share one skeleton, only the availability row changes.

#### Notification Service (clarifications only)
- **Shape:** ~20 teams send `SendNotification(userId, templateId, data, priority, idempotencyKey)` commands (one owner → command; optionally also subscribe to domain events) via a queue; the service decides whether/how (preferences, dedup, quiet hours) and sends via email/SMS/push providers.
- **Rules given:** channels email/SMS/push (+WhatsApp soon); priorities Critical (seconds, never behind promos, ignores quiet hours) / Normal / Bulk (2M promo batches, quiet hours 9 PM–8 AM local, rate-limited); transient errors → backoff ×3 + SMS fallback provider, permanent → no retry, then dead-letter; status query by ID; preferences per category, security can't be fully opted out.
- **Key points:** check preferences at SEND time → status Skipped (preference); dedup by idempotency key, not user + timestamp; guarantee = at-least-once to provider + idempotency, not exactly-once. Likely design: channel Strategy + factory, separate queues per priority, template renderer, delivery log.

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
