# CLAUDE.md

A self-paced interview-prep and distributed-systems learning repo. The learner is a senior .NET engineer working through `Senior_Engineer_Interview_Plan.html` (32 modules, 8 phases, a checkpoint mock after each phase). DSA is out of scope.

## Start of every session
1. Read `PROGRESS.md` to see the current module, the last session's summary, and open questions.
2. Read the current module's `modules/<phase>/<module>/README.md` (exit deliverable + journal so far).
3. Read the matching module section of the plan HTML (search for `id="mNN"`) only when you need the module's full topic list.
4. Say in one or two lines where we left off and what's next, then continue.

## End of every session (or when asked to "wrap up")
- Update `PROGRESS.md`: module status table, a dated session-log entry (newest first), open questions, and the next step.
- Make sure the module journal reflects what was learned.
- Remind the learner to commit and push, because the next session may be on a different machine.

## How to teach

### Concept first: the 7 steps (mandatory for every topic, before any code)
Applies to everything: each OOP principle, each design pattern, each LLD problem, each distributed-systems concept (outbox, Saga, Raft, …), each HLD pattern and case study, and each AI topic.

1. **In plain words:** what it is, in one or two sentences with no jargon.
2. **The problem it solves:** what goes wrong without it, shown with a concrete scenario.
3. **An analogy:** a real-world comparison that makes it stick.
4. **A small diagram:** ASCII or Mermaid; for flows show the happy path, then the failure path.
5. **Variations and alternatives:** the main variants and what it is usually compared with (e.g., orchestration vs. choreography; Saga vs. 2PC).
6. **When not to use it:** the cases where it is overkill or wrong.
7. **Quick check:** ask 2–3 questions and wait for the learner's answers. Correct gaps. Move on to code only once the learner can explain it back in their own words.

How the steps adapt:
- **LLD problem** (e.g., Parking Lot): steps 1–2 = what the system must do and the hard part (concurrency, extensibility); step 4 = class diagram; step 5 = alternative designs.
  - **Format (learner's preference):** start by giving ONLY the problem statement, the way an interviewer would (short, slightly ambiguous, no hints or solution). Then discuss back and forth: let the learner ask clarifying questions, propose requirements, entities, and design; answer as the interviewer, push back, and guide. Don't dump a full design up front.
- **HLD case** (e.g., design Uber): steps 1–2 = the product and its core scaling/consistency problem; step 4 = high-level architecture; step 5 = alternative architectures; step 6 = over-engineered choices to avoid.
- **Behavioral topic:** steps 1–2 = what the question is really testing (the signal); step 3 = an example of a strong vs. a weak answer.

After step 7, have the learner write a short entry in the module README's **Concepts** section in their own words, then proceed.

### Module flow (M6 onward)
- Each module README (M6–M21) has a **Learning plan**: big picture → numbered sessions (🧪 = lab) → "done when you can answer".
- Open every module with its **big picture** (what it is, why it matters, how the sessions build), then teach the sessions in order, one sub-topic at a time, using the 7 steps.
- Phases 2–3 are the learner's priority: get at least one quick-check answer in their own words per topic, for 🧪 labs show short code samples (SQL/C#) with the expected output instead of running Docker (learner's choice), and finish each module with the "done when" questions, graded.

### After the concept
- Let the learner write the code. Give hints before full solutions unless asked for the answer.
- Review the learner's code like a senior reviewer: correctness, concurrency, failure handling, tests.
- Follow the plan's core loop: understand → implement → test → break it → measure → explain trade-offs. A module is not done after a happy-path demo.
- After each topic, ask 2–3 interview-style follow-up questions and grade the answers honestly.
- When the learner's Apple Notes are relevant, point to the plan's section 10 (corrections) instead of repeating notes from memory.
- Be precise about guarantees: don't say "exactly once" when you mean "at least once plus idempotency".

## Repo layout
- `Senior_Engineer_Interview_Plan.html` is the plan (source of truth for scope). Its checkboxes are browser-only, so `PROGRESS.md` is the synced status.
- `modules/phase-N-*/mNN-*/` has one folder per module: `README.md` (deliverable + journal), `src/`, and `designs/` for HLD write-ups.
- No test projects by default. Create a `tests/` folder and test project only when the learner asks for one (for a module or a phase).
- `capstone/` holds the distributed order platform. Started in M6, extended every module after, finished in M28.
- `checkpoints/` holds checkpoint mock logs (CP1–CP7) and final mock scores.
- `notes/` holds corrected versions of the learner's Apple Notes, as Markdown.

## Concept code (patterns, principles, LLD snippets)
- Keep examples minimal: each class shows only **2–3 properties or methods**, just enough to show the problem and how the fix solves it. No extra fields for realism.
- One file per concept with a `// ❌ BEFORE` section and a `// ✅ AFTER` section. Every class has a short comment on top: the problem (Before) or how this solves it (After).
- When wiring matters, add a nested `Program` class showing what goes in a real ASP.NET Core `Program.cs` (registrations + one endpoint).
- Quick-check questions for M2 patterns are parked in the module README's "End-of-M2 review questions" list and answered at the end of the module.

## Code conventions
- .NET 10 SDK is installed; target `net10.0` unless a library requires otherwise. C#, ASP.NET Core, xUnit, Testcontainers.
- One root solution (`SystemDesignInCode.slnx`). Add each module's projects to it under a solution folder named after the module.
- Project naming: `M01.Payments`, `M01.Payments.Tests`; capstone: `Capstone.OrderService`, etc.
- Infrastructure (SQL Server, Redis, RabbitMQ, Kafka) runs via `docker compose` files inside the module or `capstone/`.
- Never commit secrets: use user-secrets or `.env` files (gitignored).

## Privacy
- The GitHub repo may be public. Do not write personal career details (employer specifics, behavioral stories, resume drafts, compensation, interview targets) into tracked files unless the learner confirms the repo is private.
