# G. Execution and quality

Amazon's Dive Deep and Insist on the Highest Standards. They test how you work when things break or time is short.

⭐ = asked in almost every loop. **Story** = the story id that answers it (❓ gap = no story yet). Back to [001-start-here](001-start-here.md); technical "defend this line" questions are in [012-resume-deep-dive](012-resume-deep-dive.md).

## General questions
| # | Question | What they're testing | Story |
|---|---|---|---|
| G1 ⭐ | The hardest bug you've debugged | Method, depth, persistence | ST16 |
| G2 ⭐ | A production incident you owned; a postmortem that led to a systemic change | Mitigation first, then root cause and prevention | ST7, ST15 |
| G3 | Tech debt vs. features; a time you cut corners | Trade-off judgment, paying it back | ST18 |
| G4 | Speed vs. quality; working under pressure | What you protect when time is short | ST1 |
| G5 | A time you dove deep to find a root cause others missed | Dive deep | ST15, ST16, ST17 |

## From my resume
| # | Question | What they're testing | Story |
|---|---|---|---|
| G6 | FedRAMP High: what was your part vs. the team's? Which control was hardest to meet? | 'I' vs. 'we', depth | ST1 |
| G7 | The four vulnerabilities: how did you prioritise under SLA pressure? Root cause and fix, generically | Prioritisation under pressure (no confidential detail) | ST1 |
| G8 | On-call for 10 services: what was it like? What broke worst? | Operational ownership | ST10 |

## Stories
_Pre-filled from the resume and notes. Fill every ❓ in your own words; keep each story under 3 minutes (Situation + Task ~20%, Action ~60%, Result + lesson ~20%)._

### ST1 · Four vulnerabilities fixed ahead of SLA during FedRAMP High (Citrix)
- **Answers:** G7, G6, B3, G4
- **Known (resume / notes):** Multi-cloud (Azure, AWS, GCP) FedRAMP High systems; helped bring the platform to FedRAMP High production-ready status; fixed 4 confirmed vulnerabilities (TLS bypass, command injection, IDOR, authorization bypass) ahead of SLA.
- **Situation:** ❓ What the SLA was (days?), who found the issues (scan, pen test, audit), what was at stake if they slipped
- **Task (my job in it):** ❓ Which of the 4 were yours alone vs. shared; who you coordinated with
- **Action (what I did: "I", not "we"):** ❓ How you ranked them (exploitability? blast radius?); each root cause + fix in one generic line, no confidential detail; how you verified each fix
- **Result (numbers):** All 4 fixed ahead of SLA; ❓ how far ahead; ❓ any systemic change (a lint rule, a review checklist, a test)
- **What I'd do differently:** ❓
- **Follow-ups to prep:** Which one was hardest and why? How did you prove the fix worked? How do you stop the same class of bug coming back?

### ST15 · Weekend search-latency incident (0-character searches) (UPMC (notes))
- **Answers:** G2, H1, G5
- **Known (resume / notes):** Notes: a weekend search-latency incident caused by 0-character searches.
- **Situation:** ❓ When it happened, who noticed, how bad
- **Task (my job in it):** ❓ On-call? Volunteered?
- **Action (what I did: "I", not "we"):** ❓ How you mitigated first, found the cause, fixed it; the postmortem
- **Result (numbers):** ❓ time to mitigate; ❓ the systemic change afterwards
- **What I'd do differently:** ❓
- **Follow-ups to prep:** How did you find it? What changed so it can't happen again?

### ST16 · Consumer vs. API deadlock, found from deadlock reports (UPMC (notes))
- **Answers:** G1, G5
- **Known (resume / notes):** Notes: a consumer vs. API deadlock, diagnosed from SQL Server deadlock reports, with the DBAs.
- **Situation:** ❓ How often, what users saw
- **Task (my job in it):** ❓ Why it was yours
- **Action (what I did: "I", not "we"):** ❓ How you read the deadlock graph, the root cause, the fix, how the DBAs helped
- **Result (numbers):** ❓ deadlocks before vs. after
- **What I'd do differently:** ❓
- **Follow-ups to prep:** Walk me through the deadlock graph. Why not just retry on deadlock?

### ST17 · MSMQ silently dropping messages → RabbitMQ + monitoring (UPMC (notes))
- **Answers:** G5, B2, E1
- **Known (resume / notes):** Notes: MSMQ silently dropping messages; moved to RabbitMQ with monitoring. Resume: NServiceBus over RabbitMQ. (Careful: the plan's 'claims to verify' flags a different MSMQ → Azure Service Bus story; tell only what you did.)
- **Situation:** ❓ How you found out messages were lost, what they were
- **Task (my job in it):** ❓ Your part
- **Action (what I did: "I", not "we"):** ❓ How you proved the drops, why RabbitMQ, how you migrated without losing messages, what monitoring you added
- **Result (numbers):** ❓ messages lost before vs. after
- **What I'd do differently:** ❓
- **Follow-ups to prep:** How did you prove messages were being dropped? How did you migrate safely?

### ST18 · WCF → Web API modernisation / refactoring a tightly coupled MVC app (UPMC (notes))
- **Answers:** G3, E2
- **Known (resume / notes):** Notes: WCF → Web API modernisation; refactoring a tightly coupled MVC app.
- **Situation:** ❓ Why it needed changing (pain, risk)
- **Task (my job in it):** ❓ Your part
- **Action (what I did: "I", not "we"):** ❓ How you refactored without stopping feature work (strangler? one endpoint at a time?)
- **Result (numbers):** ❓ what improved
- **What I'd do differently:** ❓
- **Follow-ups to prep:** How did you balance this with feature work? How did you avoid a big-bang rewrite?

_New story: copy this template._

### <Story title>
- **Answers:**
- **Situation:**
- **Task (my job in it):**
- **Action (what I did: "I", not "we"):**
- **Result (numbers):**
- **What I'd do differently:**
- **Follow-ups to prep:**
