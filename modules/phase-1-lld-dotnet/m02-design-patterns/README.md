# M2 — Design patterns through implementation

> Plan: `Senior_Engineer_Interview_Plan.html#m2` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] Explain why each pattern is appropriate, what complexity it adds, and when you would not use it.

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

#### Strategy
- **In plain words:** Keep each rule in its own class, separated behind one interface, so the code that uses it doesn't know which one it got. The right one is chosen at runtime.
- **Problem it solves:** All cases in one method/switch keep growing. A new rule means editing working code (breaks Open/Closed), rules can't be tested alone, and devs collide in the same switch. With Strategy, a new rule = a new class; the code using the rules is never edited.
- **Analogy:** Getting to the airport: taxi, train, or car. Same goal, pick the way based on the situation; the airport doesn't care which.
- **Diagram:**
  ```
  CheckoutAfter ──uses──▶ IDiscount.Apply(cart)
                               ▲          ▲
                          PercentOff   FlatOff   (+ new class, CheckoutAfter untouched)
  ```
  Flow: `Program.cs` CREATES the discount objects → `CheckoutAfter` builds a `code → object` dictionary once → per request `TryGetValue(code)` SELECTS one → `discount.Apply(cart)` CALLS it through the interface (polymorphism runs the real class).
- **Variations / alternatives:** How the strategy is picked: injected once, passed per call, or looked up from a registry/dictionary (ours). `Func<>` delegate for one-liners. Strategy vs. Template Method (composition vs. inheritance). Often paired with Factory.
- **When not to use it:** Only 2 small, stable branches (a `switch` is clearer). Algorithms that need different inputs (forces a fat interface). The variation is just data, not behavior (one class + config).
- **Quick-check answers:** Parked in "End-of-M2 review questions" below.
- **Code:** `src/M02.Checkout/DesignPatterns/Strategy.cs`

#### Factory
- **In plain words:** Keep the creation of objects (which one, and how to build it) in one separate place, so callers don't repeat it. Callers just ask the factory and use the interface.
- **Problem it solves:** Every caller (Checkout, Refunds, ...) copies the same if/else and constructor details (secrets, args). A constructor change or a new provider means editing every caller, and copies drift (Apple Pay added in Checkout, forgotten in Refunds). With a factory, that's one edit in one place.
- **Analogy:** Ordering pasta in a restaurant: you name the dish, the kitchen knows the recipe and who cooks it.
- **Diagram:**
  ```
  CheckoutAfter ─┐
  RefundsAfter  ─┴─▶ IPaymentProviderFactory.Create("Card") ─▶ new StripeProvider(settings.StripeApiKey)
                                         "PayPal" ─▶ new PayPalProvider(...)   unknown ─▶ throw
  ```
- **Variations / alternatives:** Simple factory (one method with a switch, ours). Factory Method (subclass overrides `Create`). Abstract Factory (a family of matching objects). Registry (`Dictionary<string, Func<T>>`) or .NET keyed services instead of a switch. DI container itself is a factory.
- **When not to use it:** One implementation and one caller (`new` or plain DI is fine). The type is known at startup: DI already does the job, inject `IPaymentProvider` directly. Still needed when the choice or args come from runtime data (the request).
- **Key points:** Hiding secrets is a side effect; the main job is hiding *which class* and *how it's built*. Strategy = what the object **does**; Factory = how it's **created**. They pair up when the choice is at runtime and objects are non-trivial to build.
- **Quick-check answers:** Parked in "End-of-M2 review questions" below.
- **Code:** `src/M02.Checkout/DesignPatterns/Factory.cs`

#### Builder
- **In plain words:** For an object that's assembled in steps and has rules, create a separate builder class with readable public methods (one per param) that callers chain. A final `Build()` checks everything and is the only way to create the real object.
- **Problem it solves:** A big constructor: unclear call sites (`new OrderBefore("c1", [], null)`), easy-to-swap args, `null` for optional values, and nothing stops an invalid object (no items) until it fails far away.
- **Analogy:** Sandwich counter: add bread, turkey, cheese step by step, skip what you don't want; you only get the sandwich at the end, and never one without bread.
- **Diagram:**
  ```
  Order.Create("c1") → OrderBuilder (mutable, collecting)
     .AddItem("SKU1")
     .WithDiscount("FLAT20")   (optional, skip if absent)
     .Build()                  → checks rules → Order (read-only, always valid)
  Failure: Order.Create("c1").Build() → no items → InvalidOperationException at Build()
  ```
- **Variations / alternatives:** Fluent builder (ours; `WebApplication.CreateBuilder`, `StringBuilder`, `UriBuilder`). GoF builder with a Director (rare). Test Data Builder (very common in tests). Simpler: `record` + `required` members, named/optional args, Parameter Object (groups params, but no steps and no `Build()` gate).
- **When not to use it:** DTOs / request-response objects (the JSON serializer builds them; use a record, validate at the API boundary). All data available at once: a constructor that validates is enough. Need BOTH: rules AND built in pieces. Never register a builder as a DI singleton (stateful, single-use).
- **Key points:** Validation is the gate at the end; step-by-step assembly is why the pattern exists. Product has a private constructor and copies the builder's data.
- **Quick-check answers:** Parked in "End-of-M2 review questions" below.
- **Code:** `src/M02.Checkout/DesignPatterns/Builder.cs`

#### Singleton
- **In plain words:** One shared instance for the whole app. In .NET: a normal class registered with `AddSingleton`, not a hand-written `static Instance`.
- **Problem it solves:** Many copies of shared state (a cache) → repeated API calls, inconsistent answers, wasted memory.
- **Analogy:** One shared printer queue for the whole office.
- **Key points:** Must be thread-safe (`ConcurrentDictionary`); dev "works" only because requests don't overlap. `DbContext` is scoped, never singleton. `HttpClient` via `IHttpClientFactory` (per-call `new` → socket exhaustion; raw static → stale DNS). No singleton → scoped dependency (captive dependency). One instance per **process**; across servers use Redis. Static is for stateless/immutable things you'd never mock (`JsonSerializerOptions`, `Regex`, extension methods).
- **Code:** `src/M02.Checkout/DesignPatterns/Singleton.cs`

#### Adapter (+ Facade)
- **In plain words:** A wrapper that makes a class with the wrong interface fit OUR interface. It only translates (types, units, exceptions); no business logic.
- **Problem it solves:** Vendor types, units, and exceptions leak into business code; swapping vendors means a rewrite; can't test without the SDK.
- **Analogy:** Travel plug adapter: changes the shape, not the electricity.
- **Key points:** Not only for SDKs: legacy code, another team's API, V1→V2 migrations. Same shape as Strategy, different reason: a strategy IS the logic, an adapter wraps someone else's. Facade = one simple entry point over MANY classes.
- **Code:** `src/M02.Checkout/DesignPatterns/Adapter.cs`

#### Observer
- **In plain words:** When something happens, notify a list of subscribers without knowing who they are.
- **Problem it solves:** The publisher calls every side effect directly and grows with each new reaction.
- **Analogy:** YouTube channel and its subscribers.
- **Key points:** Each handler's failure is caught so it can't fail the main action. In-process = best-effort: a crash after the save means no handler runs → Outbox + broker for guarantees. Pub/sub = Observer across processes with a broker (durable, async, at-least-once → idempotent handlers). Observer reacts to what HAPPENED; it's the wrong tool for a compliance audit of what was REQUESTED (use Command).
- **Code:** `src/M02.Checkout/DesignPatterns/Observer.cs`

#### Command
- **In plain words:** Turn a request into an object (a record), so it can be logged, queued, retried, or undone.
- **Problem it solves:** Method calls are gone once made; cross-cutting code (audit) gets copy-pasted into every operation.
- **Analogy:** A restaurant order ticket.
- **Key points:** Record + handler + dispatcher (MediatR style); audit lives once in the dispatcher and also records failed attempts. Classic GoF = `Execute`/`Undo` + history stack. NServiceBus `Send` = async command to ONE owner (caller gets 202, handler must be idempotent); `Publish` = event to MANY (Observer). Overkill for plain CRUD.
- **Code:** `src/M02.Checkout/DesignPatterns/Command.cs`

#### Chain of Responsibility
- **In plain words:** Pass a request along a line of handlers; each one stops it (returns the error) or passes it on.
- **Problem it solves:** One huge `Validate()`: can't reorder, reuse, or test checks individually.
- **Analogy:** Airport security stations.
- **Key points:** The request goes forward unchanged; the result comes back through the returns. Cheap checks first, slow external calls last. Linked (`SetNext`) or list/loop style. ASP.NET Core middleware is CoR (`[Authorize]` is just metadata the authorization middleware reads). If the user needs ALL errors at once, collect them instead (FluentValidation).
- **Code:** `src/M02.Checkout/DesignPatterns/ChainOfResponsibility.cs`

_Dropped by choice: State, Decorator._

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

#### Mock round 2 (2026-11-16): ~4.6 / 10 (harder questions)
- Strong: Q4 Builder (cross-field rule in `Build()`), Q9 Factory vs. plain DI.
- Revisit: **Q3** reject a generic `IRepository<T>` over EF; approve only a specific repository with named queries. **Q5** "must never be skipped" → event (pub/sub) + Outbox + broker + idempotent handlers; a command has ONE owner, many reactions = event. **Q10** `SaveChanges` is one DB transaction but does NOT cover a RabbitMQ publish (two systems, no shared transaction) → Outbox.
- Lessons: constraint words ARE the question; be skeptical of extra layers (name what they add); DB write + message publish together → say "Outbox".
- Also: Q6 captive dependency (singleton holding scoped `DbContext`); Q8 collect-all validation still rejects, it just reports every error.

#### Mock round 1 (2026-11-16): ~6.6 / 10
- Strong: pattern recognition; Q6 (CoR middleware), Q8 (Strategy + Factory), Q10 (event vs. command).
- Missed: Q5 compliance audit of admin actions → **Command** (audit in the dispatcher, before the handler, records failures too), not Observer.
- Habits to practise: (1) answer the scenario's specific constraint ("user preference", "only 1–2 fields", "compliance"); (2) lead with the root cause (shared state, thread safety); (3) shape every answer as pattern → how it works here → one trade-off/failure.
- Corrections to remember: the factory RETURNS the strategy, the caller calls it. An adapter only translates, no domain logic. Test Data Builder = defaults for all fields. A singleton builder mixes data even without concurrency.

#### End-of-M2 review questions (answer after all patterns are covered)
_Parked here while going through the patterns, answered together at the end of the module._

**Strategy**
1. Marketing wants discounts to **stack** (10% off, then $20 flat). Does the `CheckoutAfter` design handle that? What would you change or add? _(hint: a "composite" discount that holds several `IDiscount`s and is itself an `IDiscount`)_
2. `CheckoutAfter` builds a dictionary from `IEnumerable<IDiscount>`. What happens with a duplicate code or an unknown code, and why were those behaviors chosen?
3. When would you **not** use Strategy, and use a plain `switch` instead?
4. Where should the strategy lookup (code → object) live: the context, `Program.cs`, or the caller? Why not `Program.cs`?

**Factory**
1. Strategy vs. Factory: which part does each one handle, and why do they often appear together?
2. `PaymentProviderFactory` has a `switch`. Isn't editing it for Apple Pay an Open/Closed violation? Why is it acceptable, and how would you remove it?
3. .NET has a DI container. Why inject `IPaymentProviderFactory` instead of `IPaymentProvider` directly?
4. In Strategy, `Program.cs` creates the objects. In Factory, it doesn't. Why the difference?
5. Simple factory vs. Factory Method vs. Abstract Factory: one-line difference and an example of each.

**Builder**
1. Builder vs. a `record` with `required` properties: when is the builder worth the extra class?
2. Why must a builder never be registered as a DI singleton? What exactly goes wrong?
3. Why does `Order`'s constructor copy `Lines` instead of keeping the builder's list?
4. Name three Builders in .NET itself.

**Singleton**
1. Why is `_instance ??= new()` not thread-safe? Walk through two threads.
2. Classic `X.Instance` vs. `AddSingleton`: why is the DI version preferred?
3. What is a captive dependency? Give an example and how ASP.NET Core can catch it (hint: scope validation).
4. A DI singleton holds a `Dictionary`. What can go wrong under load, and what's the fix?
5. Why must `DbContext` be scoped and never a singleton? Give at least three failure modes.
6. `new HttpClient()` per request vs. a static `HttpClient` vs. `IHttpClientFactory`: what goes wrong with the first two?
7. A singleton holds state shared by 3 API servers. Why doesn't it work, and what do you use instead?

**Adapter (+ Facade)**
1. Adapter vs. Facade: one line each on intent.
2. Why should the adapter translate the vendor's exceptions too, not just its types?
3. Someone adds retries and caching inside `FedExAdapter`. What's wrong, and where should they go?
4. How does Adapter relate to the Anti-Corruption Layer in DDD?


**Observer**
1. One observer throws. Should the order fail? What did we choose and why?
2. The process crashes right after saving the order. What happens to the email, and what pattern fixes it?
3. C# `event` vs. injected `IEnumerable<IHandler>`: trade-offs, and the event memory-leak pitfall.
4. Observer (in-process) vs. pub/sub with a broker: what does the broker add?
5. When should a reaction NOT be an observer but a direct call?

**Command**
1. What can you do with a command object that you can't do with a method call? Name three.
2. Classic GoF Command (`Execute`/`Undo`) vs. the record + handler + dispatcher style: when would you use each?
3. MediatR: what does it give you, and what's the common criticism of using it everywhere?
4. Command vs. Strategy: same "object with one method" shape, so what's the difference?
5. NServiceBus `Send` (command) vs. `Publish` (event): which pattern is each, and what changes when a command becomes async?

**Chain of Responsibility**
1. Why put the fraud check last? What principle decides the order?
2. Linked chain (`SetNext`) vs. injected `IEnumerable<ICheck>` loop: trade-offs?
3. A form must show ALL validation errors at once. Is CoR still the right fit? What would you use?
4. How is ASP.NET Core middleware an example of this pattern?
5. What actually enforces `[Authorize]`, and why is that Chain of Responsibility?

**Repository / Unit of Work on EF Core**
1. "Would you put a repository on top of EF Core?" Give the senior answer (not "always", not "never").
2. Which EF Core types already implement Repository and Unit of Work, and how?
3. What's wrong with a generic `IRepository<T>` that returns `IQueryable<T>`?
4. "We need repositories so we can mock the database in tests." How do you respond?
5. Why must `DbContext` (the unit of work) be scoped per request?


### My notes
_Which Apple Notes did this update or correct?_
