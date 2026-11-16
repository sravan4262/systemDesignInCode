# M1 — OOP, SOLID, and clean design

> Plan: `Senior_Engineer_Interview_Plan.html#m1` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Add a new payment provider without modifying the core payment workflow. Explain where abstractions help and where they become overengineering.

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
_Code for each principle (violation vs. fix): `src/M01.Payments/SolidPrinciples/`._

#### Encapsulation
- **In plain words:** Keep data private; change it only through methods that enforce the rules.
- **Problem it solves:** Public setters let any caller create invalid state (negative amount, refunding an uncaptured payment); rules get copied into every caller.
- **Analogy:** An ATM: you can't open the vault, you go through buttons that check your balance.
- **Key detail:** `readonly` / getter-only protects the *reference*, not the *contents*. Expose `IReadOnlyList<T>` via `AsReadOnly()`, keep the mutable list private, and make items immutable (records).
- **When not to use it:** DTOs and API models just carry data; a get/set property that does nothing isn't encapsulation.

#### S — Single Responsibility
- **In plain words:** A class has one reason to change (answers to one stakeholder), not "one public method".
- **Problem it solves:** A class that validates, charges, saves, emails, and audits breaks charging when marketing changes the email.
- **Analogy:** Restaurant: chef cooks, waiter serves, cashier bills.
- **Key detail:** A thin controller that delegates to a service is SRP done right; a fat controller with business logic is the violation.
- **When not to use it:** Don't split code that always changes together (class explosion).

#### O — Open/Closed
- **In plain words:** Add behavior by adding a class, not by editing tested code.
- **Problem it solves:** A `switch` on payment method means every new provider edits (and re-tests) the core class.
- **Analogy:** A power strip: plug in new devices without rewiring the house.
- **Key detail:** Each provider declares its own key (`Method`); a dictionary is built at startup from DI registrations; lookup happens per request. The DI registration in `Program.cs` (composition root) is the one accepted place that names concrete types.
- **When not to use it:** Closed sets (days of the week) — keep the `switch`. Don't abstract before a second variation exists (rule of three).

#### Abstraction & Polymorphism
- **Abstraction:** Exposing *what* something does and hiding *how*; in C# usually an interface (`IPaymentProvider`).
- **Polymorphism:** One interface, many implementations; the same call `provider.Charge()` runs different code depending on the actual object at runtime (dynamic dispatch).
- **Together:** They make OCP work — the caller depends only on the interface and never names a concrete provider.

#### L — Liskov Substitution
- **In plain words:** Any subtype must work wherever its base type is expected.
- **Problem it solves:** `BankTransferProvider.Refund()` throws; `RefundService` crashes though the caller did nothing wrong.
- **Analogy:** A rental car whose brake pedal accelerates is still "a car", but you can't substitute it.
- **Key detail:** Changing *how* is polymorphism; breaking *what was promised* is the violation. Rules: don't strengthen preconditions (wallet min $1), don't weaken postconditions, keep invariants, don't throw unexpected exceptions. `ReadOnlyCollection<T>` implementing `IList<T>` is a known violation (kept for compatibility).
- **Fix:** Split the interface (`IRefundableProvider`) so the compiler prevents the misuse.

#### I — Interface Segregation
- **In plain words:** Don't force a class to depend on methods it doesn't use.
- **Problem it solves:** A fat provider interface forces Apple Pay to throw on subscriptions, and makes `RefundService` depend on (and fake) five methods to use one.
- **Analogy:** A 60-button remote for a TV that supports three.
- **Key detail:** Split by what *clients* need (role interfaces); one class can implement many. A fat interface forces classes to implement methods they can't honor → they throw → LSP breaks. ISP prevents that.
- **Example:** `IReadRepository<T>` / `IWriteRepository<T>`; reporting depends only on reads (and can point at a read replica).
- **When not to use it:** One method per interface everywhere ("interface soup"); one client and one implementation.

#### D — Dependency Inversion
- **In plain words:** Business code and technical details both depend on an abstraction owned by the business side.
- **Problem it solves:** `PaymentService` creating `StripeClient` directly can't be tested or switched to Adyen without editing business logic.
- **Analogy:** The building defines the socket standard; appliance makers build plugs to fit it.
- **Key detail:** Injection ≠ inversion — injecting a concrete `StripeClient` still violates DIP. The interface lives in the Application/Domain project; Infrastructure references Application, so the dependency arrow is inverted. Composition (*has a* gateway) over inheritance (*is a* StripeBase).
- **When not to use it:** Stable types (`string`, `List<T>`), throwaway scripts, interfaces created only for mocking pure in-memory classes.

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
