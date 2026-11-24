# G. Execution and quality

Amazon's Dive Deep and Insist on the Highest Standards. They test how you work when things break or time is short.

⭐ = asked in almost every loop. **Story** = the story id that answers it (❓ gap = no story yet). Back to [001-start-here](001-start-here.md); technical "defend this line" questions are in [012-resume-deep-dive](012-resume-deep-dive.md).

## General questions
| # | Question | What they're testing | Story |
|---|---|---|---|
| G1 ⭐ | The hardest bug you've debugged | Method, depth, persistence | ST22 (learner's pick 2026-11-24: Azure AI Search migration tool missing records), ST16 |
| G2 ⭐ | A production incident you owned; a postmortem that led to a systemic change | Mitigation first, then root cause and prevention | ST24 (draft 8/10), ST7, ST15 |
| G3 | Tech debt vs. features; a time you cut corners | Trade-off judgment, paying it back | ST18 |

## From my resume
| # | Question | What they're testing | Story |
|---|---|---|---|
| G7 | FedRAMP High and the four vulnerabilities: your part vs. the team's; how did you prioritise under SLA pressure? Root cause and fix, generically | Prioritisation under pressure (no confidential detail) | ST1 |

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
- **My answer for G7 (my words, 2026-11-24, saved as is):**
  > A few of the vulnerabilities involving our team were nearing SLA, which is not FedRAMP compliant. My job was to fix these vulnerabilities, or create DRs if they couldn't be fixed.
  >
  > I started looking into them and identified that three were related and the other one wasn't. I started with the three related ones, because if I could identify the fix I could knock out three instead of one; quantification mattered. It was a vulnerability reported in one of the .NET frameworks, and a few containers were using the same version, which caused it to be reported in those three containers. I looked at the patch fixes, saw it was fixed by Microsoft and not reported in the fixed version, so I updated the version for those three containers, deployed them to staging first, waited for the vulnerability scan to run and verified the fixes worked. They did, so I deployed to prod immediately with a high-priority prod deployment ticket.
  >
  > The other one had a vendor dependency and couldn't be fixed. It's a known CVE and had had a DR for a while. First I checked: if it had a DR, why was it showing up now? I looked at the data, it was normal; I looked at the DR, it seemed normal; then I checked whether the vulnerability scan was taking the DR into account, and it was. I looked at the DR once again and realised the scan only considers DRs created within a certain past period. So I let the team know immediately, created a new DR, waited for the run, and fixed this within the SLA.
- **Grade (G7):** 7/10. Strong: grouping the 3 related findings first (one fix closes three), staging → scan-verified → expedited prod, and root-causing why the 4th reappeared (the scanner only honours DRs inside a time window).
- **⚠️ Resume mismatch to resolve:** the resume line names TLS bypass, command injection, IDOR and authorization bypass (code-level flaws); this answer is framework CVEs in containers + a lapsed vendor-CVE DR. Either prep the code-level story too, or reword the resume line to match this one.
- **To strengthen (❓):**
  - Explain "DR" (deviation request: FedRAMP's formal risk acceptance, tracked in the POA&M).
  - Numbers: the SLA (days), days left when you started, how far ahead you finished.
  - Upgrade risk: what you checked beyond the scan (release notes, tests, smoke tests in staging).
  - Systemic fix: a shared base image (one bump fixes all), automated dependency updates + image scanning in CI; DR expiry tracking with an alert before lapse.
  - The DR justification (compensating control, vendor timeline).
  - "I let the team know": who, and why (other DRs about to lapse?).

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

### ST24 · On-call: queue consumer crashing on startup after a deploy (empty Redis connection-string secret) (❓ employer)
- **Answers:** G2 ⭐ (also G1, G5; "a time you improved a process")
- **Status:** first draft in my words, saved as is (2026-11-24).
- **My answer (my words):**
  > I received an alert that one of the message-queue consumers wasn't processing: the queue was growing and users weren't seeing the data. I was on call, so I got paged and jumped into the incident call.
  >
  > I started going through the logs. I didn't see any recent failures in App Insights. Then I checked the queue to see whether these were genuine failures or transient ones: there were no messages in the dead-letter queue, just messages piling up, with no clear indication of what was going on.
  >
  > I ran the health check first and saw failures. Then I looked at the server logs and understood that the service wasn't running: it was crashing on startup. It had retried a few times and then stopped completely. The logs showed a missing config that stopped it from starting.
  >
  > I immediately checked for a recent deployment and found one. I went through the commit history to see if there was something obvious I could mitigate quickly, and found the config that had been added. I checked it against the prod variable group and saw it there, but I couldn't see the actual value because it was a secret and I didn't have permission. So we brought in a member of the responsible team, and we found the value was empty: it was the Redis connection string. We immediately added it to Key Vault and redeployed the service. Meanwhile, messages kept piling up.
  >
  > Once it was redeployed, the messages started processing, and within a few minutes all of them were processed successfully.
  >
  > I put this in the incident response. When the incident was reviewed later, I provided all the information and suggested that we don't assume a completed deployment means the service is up and running: run health checks after the deployment, monitor the service for a few minutes and check for runtime issues, and only then mark the deployment as complete. This was later added to all release requests, so these steps are completed before a deployment is marked as done.
- **Grade:** 8/10 (2026-11-24). Systematic narrowing (app logs → DLQ → health check → server logs → crash on startup → recent deploy → commit → config), brought in the right person, and a systemic change that was adopted for every release.
- **To strengthen (❓):**
  - **Mitigation first:** the interviewer will ask "why not roll back to the previous release right away?" The previous version didn't need the new config, so a rollback would likely have restored processing in minutes while you investigated. Either say why you didn't (e.g. the release had other changes that couldn't be rolled back, or a schema change), or make that your "what I'd do differently".
  - **Numbers:** time from page to mitigation, how far the queue grew, how many users were affected, how long the drain took.
  - **Communication:** who you updated during the incident, and how often.
  - **Deeper prevention**, which is stronger than a release checklist:
    - **fail fast on bad config:** validate required settings at startup (`ValidateOnStart`), so a missing value fails the deployment instead of crashing later;
    - **gate the release on health:** readiness checks or a slot swap only after `/health` passes;
    - **pipeline check:** a secret must have a value before it can be referenced.
  - **Why the secret was empty:** a process gap (added to the variable group without a value?). Worth one sentence.
  - **"I" vs. "we":** "we added it to Key Vault": say who did what.
- **Follow-ups to prep:** Why not roll back? Why did the deployment report success while the service was crashing? How would you catch this before prod next time? What did users see, and was any data lost (no: messages waited in the queue, so durable queues saved you)?

_New story: copy this template._

### <Story title>
- **Answers:**
- **Situation:**
- **Task (my job in it):**
- **Action (what I did: "I", not "we"):**
- **Result (numbers):**
- **What I'd do differently:**
- **Follow-ups to prep:**
