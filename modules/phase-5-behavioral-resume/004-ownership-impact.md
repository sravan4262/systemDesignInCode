# B. Ownership, impact, delivering results

Amazon's Ownership, Deliver Results, Bias for Action, Invent & Simplify, Frugality; Meta's 'scope' and 'ownership'. The most-asked section: have at least 3 strong stories here.

⭐ = asked in almost every loop. **Story** = the story id that answers it (❓ gap = no story yet). Back to [001-start-here](001-start-here.md); technical "defend this line" questions are in [012-resume-deep-dive](012-resume-deep-dive.md).

## General questions
| # | Question | What they're testing | Story |
|---|---|---|---|
| B1 ⭐ | The largest-scope project you drove end to end | Scope, ownership, impact with numbers | ST8, ST10, ST14 |
| B2 | A time you went beyond your role | Ownership | ST3, ST17 |
| B3 ⭐ | A missed deadline, or delivering under a tight one | Prioritisation, communication, honesty | ST1 |
| B4 | A decision with incomplete data; a calculated risk | Bias for action, judgment | ST2, ST8, ST19 |
| B5 | Something you did without your manager's approval | Judgment about when to act alone |  |
| B6 | A process you simplified or automated | Invent & simplify, a measurable result | ST3, ST4, ST5, ST6, ST12 |
| B7 | A feature that measurably improved users' lives | Customer impact, metrics | ST14 |
| B8 | Delivering with fewer resources than you needed | Frugality, trade-offs | ST2, ST3 |
| B9 | Choosing between two technologies | Criteria, evidence, the decision and its cost | ST4, ST9 |

## From my resume
| # | Question | What they're testing | Story |
|---|---|---|---|
| B10 | Azure AI Search migration: how did you make the case? How did you validate with load tests? Any surprises? | Data-driven decision, risk reduction | ST8 |
| B11 | Owning 10 services, 25 workers and 8 databases: how did you prioritise? What broke worst? | Ownership at scale | ST10 |
| B12 | Cost-cleanup jobs: how did you find the waste, make deletion safe, and get buy-in? | Frugality, safety, influence | ST3 |
| B13 | VM → Kubernetes migration: how did you reduce risk? Rollback plan? What went wrong? | De-risking, honesty | ST2 |
| B14 | RegScale POC: how did you evaluate the vendor? Build vs. buy? | Evaluation criteria, judgment | ST4 |
| B15 | Byte AI: why build it? How did you prioritise solo? Hardest call? What would you redo? | Bias for action, product sense | ST19 |

## Stories
_Pre-filled from the resume and notes. Fill every ❓ in your own words; keep each story under 3 minutes (Situation + Task ~20%, Action ~60%, Result + lesson ~20%)._

### ST2 · 4 Windows VMs → Linux Kubernetes pods (~$20K/yr) (Citrix)
- **Answers:** B13, B8, B4
- **Known (resume / notes):** Migrated 4 Windows VMs to lightweight Linux Kubernetes pods; removed Windows licensing and idle compute; ~$20K/year saved.
- **Situation:** ❓ What ran on the VMs, why it was Windows, who depended on it
- **Task (my job in it):** ❓ Your idea or assigned? Solo or with whom?
- **Action (what I did: "I", not "we"):** ❓ How you ported it to Linux, how you de-risked (parallel run? canary?), the rollback plan, what you monitored after the switch
- **Result (numbers):** ~$20K/year; ❓ any reliability or deploy-time change; ❓ what went wrong during the move
- **What I'd do differently:** ❓
- **Follow-ups to prep:** Show the $20K math. What was the rollback plan? What broke? Why pods and not Azure Container Apps / Functions?

### ST3 · Cleanup jobs for orphaned Azure images (97% cut, ~$34K/yr combined) (Citrix)
- **Answers:** B12, B6, B2, B8
- **Known (resume / notes):** Scheduled TeamCity jobs that delete orphaned Azure managed images and Compute Gallery versions; managed-image cost $2.1K → $62/month (97%); ~$34K/year combined.
- **Situation:** ❓ How you noticed the waste (cost report? curiosity?), how long it had been growing
- **Task (my job in it):** ❓ Was this part of your job, or did you go beyond it?
- **Action (what I did: "I", not "we"):** ❓ How you decided an image was 'orphaned', how you made deletion safe (dry run, allow-list, retention, soft delete?), how you got sign-off
- **Result (numbers):** $2.1K → $62/month; ~$34K/year combined (❓ say which items make up the $34K)
- **What I'd do differently:** ❓
- **Follow-ups to prep:** How did you make sure you never deleted an image still in use? Who approved it? What did 'combined' include?

### ST4 · FedRAMP asset inventory: a ~14K-resource script, then a RegScale POC (Citrix)
- **Answers:** B14, B9, E2, B6
- **Known (resume / notes):** Python automation (Windows Task Scheduler) that inventories ~14K Azure resources to feed FedRAMP asset inventory and compliance reporting; delivered a RegScale proof of concept for the same inventory.
- **Situation:** ❓ Why the inventory mattered for FedRAMP, how it was done before
- **Task (my job in it):** ❓ Your part in the script vs. the POC; who decided build vs. buy
- **Action (what I did: "I", not "we"):** ❓ How the script works (APIs, schedule, failure handling); how you evaluated RegScale (criteria, cost, effort); the recommendation you made
- **Result (numbers):** ❓ time saved / accuracy gained; ❓ the build-vs-buy outcome
- **What I'd do differently:** ❓
- **Follow-ups to prep:** Why Windows Task Scheduler and not Azure Automation or Functions? What happens when the script fails at 3 am? Build vs. buy: what tipped it?

### ST8 · On-prem wildcard search → Azure AI Search (>98% latency cut) (UPMC)
- **Answers:** B10, B1, A5, B4, H2
- **Known (resume / notes):** Led migration of a poorly performing on-prem wildcard search to Azure AI Search; sub-second results on large text fields; >98% latency reduction, load-tested. Notes: multi-table event ordering (a 10-table document kept consistent under concurrent events), a custom sync tool instead of Azure Data Factory, a vector/NLP re-index. This is also deep dive #1 in M30.
- **Situation:** ❓ Who used the search, how slow it was (p50/p95 before), how much data
- **Task (my job in it):** ❓ Did you propose it? Lead it? How many people?
- **Action (what I did: "I", not "we"):** ❓ How you made the case; the sync tool and why not ADF; how you kept the document consistent; how you load-tested
- **Result (numbers):** >98% latency reduction, sub-second; ❓ from X s to Y ms, at what load; ❓ the effect on users
- **What I'd do differently:** ❓
- **Follow-ups to prep:** From what to what, p50 or p99, at what load? Why not ADF? How did you order events across 10 tables? What surprised you?

### ST9 · Solr vs. Azure AI Search: choosing the technology (UPMC)
- **Answers:** B9, E2, F2
- **Known (resume / notes):** Resume: deployed Solr Cloud on Red Hat OpenShift with Docker. Notes: the Solr vs. Azure AI Search decision.
- **Situation:** ❓ Why a decision was needed (Solr pain? cloud move?)
- **Task (my job in it):** ❓ Who decided; your part
- **Action (what I did: "I", not "we"):** ❓ The criteria you compared (ops effort, cost, features, team skills); how you tested; how you got agreement
- **Result (numbers):** ❓ the decision and what it cost
- **What I'd do differently:** ❓
- **Follow-ups to prep:** What did you give up by leaving Solr? Would you choose the same today?

### ST10 · Owning 10 microservices, 25 workers and 8 databases (UPMC)
- **Answers:** B11, G8, B1
- **Known (resume / notes):** Owned 10 microservices, 25 worker services and 8 databases end to end; back-end services and REST APIs in C#, .NET 8, ASP.NET Core; SQL Server, Cosmos DB, Redis; NServiceBus over RabbitMQ.
- **Situation:** ❓ What the services did for the business
- **Task (my job in it):** ❓ 'Owned' = what exactly: build, deploy, on-call, roadmap? Shared with whom?
- **Action (what I did: "I", not "we"):** ❓ How you prioritised across them; what on-call was like; the worst thing that broke and how you handled it
- **Result (numbers):** ❓ uptime / incident numbers; ❓ one improvement you made
- **What I'd do differently:** ❓
- **Follow-ups to prep:** What does 'owned' mean here? What broke worst? How did you prioritise 43 things?

### ST12 · CI/CD in Azure DevOps: deploys ~8–10 min → under 2 (UPMC)
- **Answers:** B6, E1
- **Known (resume / notes):** Implemented CI/CD pipelines in Azure DevOps (YAML), deployed to Azure App Service; deployment time ~8–10 min → under 2.
- **Situation:** ❓ What deploys looked like before
- **Task (my job in it):** ❓ Your idea? Who else?
- **Action (what I did: "I", not "we"):** ❓ What made deploys slow and what you changed
- **Result (numbers):** ~8–10 min → under 2; ❓ deploys per week
- **What I'd do differently:** ❓
- **Follow-ups to prep:** What exactly was slow? What did the 8 minutes cost the team?

### ST19 · Byte AI: building and launching a product solo (Side project)
- **Answers:** B15, C1, C2, B4, F3, H3, A5
- **Known (resume / notes):** Built and shipped a tech-focused social platform end to end (~50 daily active users): .NET 9 API, Next.js web, native SwiftUI iOS, PostgreSQL/Supabase, SignalR chat, APNs push; hybrid search (ONNX embeddings + pgvector + full-text, RRF); a 'For You' feed by cosine similarity to interest embeddings; Gemini tagging + two-layer moderation; Azure blue-green releases. Also deep dive #2 in M30.
- **Situation:** ❓ Why you built it
- **Task (my job in it):** ❓ Solo: every decision was yours
- **Action (what I did: "I", not "we"):** ❓ How you prioritised with no team; the hardest call; something new you learned fast (SwiftUI?)
- **Result (numbers):** Live on the App Store and web, ~50 DAU (say it plainly; never inflate)
- **What I'd do differently:** ❓
- **Follow-ups to prep:** Why build it? What would you redo? ~50 DAU: what does that tell you, and what would you try next?

_New story: copy this template._

### <Story title>
- **Answers:**
- **Situation:**
- **Task (my job in it):**
- **Action (what I did: "I", not "we"):**
- **Result (numbers):**
- **What I'd do differently:**
- **Follow-ups to prep:**
