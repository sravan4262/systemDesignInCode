# D. Conflict and collaboration

Every loop asks at least one. They test whether you disagree with data and respect, reach a resolution, and can commit to a decision you argued against.

⭐ = asked in almost every loop. **Story** = the story id that answers it (❓ gap = no story yet). Back to [001-start-here](001-start-here.md); technical "defend this line" questions are in [012-resume-deep-dive](012-resume-deep-dive.md).

## General questions
| # | Question | What they're testing | Story |
|---|---|---|---|
| D1 ⭐ | A disagreement with a peer | Data over opinions, respect, a resolution | D1 answer (same project as F2 / ST2) |
| D2 ⭐ | A disagreement with your manager or a more senior engineer | Pushing back respectfully; disagree and commit | D2 answer (ST9 → ST8) |
| D3 | Two teams that couldn't agree on a path | Cross-team influence | skipped |
| D4 | A difficult or non-technical stakeholder | Empathy, translating tech to business | D4 answer (Splunk, F3) |
| D5 ⭐ | Critical feedback you received, and what you changed | Coachability | D5 answer (also A7) |
| D6 | Negative feedback you gave to someone | Directness with care | D6 answer (also E6, E4) |
| D7 | Working with someone whose style was very different from yours | Adaptability | D7 answer (VM migration) |

## From my resume
| # | Question | What they're testing | Story |
|---|---|---|---|
| D8 | A disagreement with your TrueSpend co-founder | Conflict, disagree and commit | skipped |

## Stories
_Pre-filled from the resume and notes. Fill every ❓ in your own words; keep each story under 3 minutes (Situation + Task ~20%, Action ~60%, Result + lesson ~20%)._

### ST21 · A disagreement with my TrueSpend co-founder (Side project)
- **Answers:** D8, D2, A14
- **Known (resume / notes):** Co-founder of TrueSpend (geo-based credit-card rewards optimiser; React Native + .NET 9; Plaid, Stripe, Azure OpenAI; in development).
- **Situation:** ❓ What the disagreement was about
- **Task (my job in it):** ❓ Your position
- **Action (what I did: "I", not "we"):** ❓ How you argued it (data?), how it was resolved, disagree and commit?
- **Result (numbers):** ❓ the outcome and what you learned
- **What I'd do differently:** ❓
- **Follow-ups to prep:** What did your co-founder think, in their words? Would you decide the same today?

### D1 · Windows vs. Linux containers for the VM migration (Citrix, same project as F2 / ST2)
- **Answers:** D1 ⭐ (same story as F2: in one loop, use it for only one of them)
- **My answer (my words):**
  > During the VM migration to Kubernetes, I proposed migrating these Windows VMs to Windows-based containers. A teammate suggested that for these particular VMs, we should migrate to Linux-based containers instead.
  >
  > I first made sure I understood his suggestion, and we agreed we were both trying to achieve the same goal. His suggestion was about achieving it with less cost: instead of creating separate Windows-based node pools, we could use the Linux-based pools we were already running. My suggestion was about achieving it with less downtime, fewer code changes, and not having to worry about breaking dependencies.
  >
  > I did some research, created a small prototype, deployed it to staging and ran some tests. It showed this could be done with zero downtime, without too many code changes, and definitely without breaking any dependent callers, on the Linux-based pools we already had. The prototype proved it, so I re-proposed migrating these VMs to Linux-based containers.
  >
  > We still work very closely, and I always value his opinion.
- **Follow-ups to prep:** What did you disagree on most? What would you have done if the prototype had failed? How did you tell the rest of the team? What code did change? (Keep this consistent with F2.)
- **Grade:** 6/10 → 7/10 after adding the shared goal and both trade-offs (2026-11-24): evidence-based and respectful.

### D2 · Solr vs. Azure AI Search, raised in the design review (UPMC, ST9 → ST8)
- **Answers:** D2 ⭐ (also F2, B9, E2; the migration itself is ST8 in [004-ownership-impact](004-ownership-impact.md))
- **My answer (my words):**
  > We were planning to migrate our search data to Azure AI Search. The team was already using Solr, but the expectation was to move to Azure AI Search, and a platform engineer was pushing for it.
  >
  > I raised my concern in the design review: the team was already using Solr, it was working well, and all the infrastructure was set up across every environment, so why move to a new tool altogether? It would be the team's first time using Azure AI Search, which meant a big learning curve and a lot of development and testing time.
  >
  > The platform engineer gave his reasons for Azure AI Search: it's a fully managed service, while Solr needs a lot of infrastructure management across multiple instances in every environment. We kept seeing issues with Solr around maintaining specific Java versions and runtimes, and being open source, it had vulnerability issues to keep patching.
  >
  > After discussing the trade-offs, I looked at the numbers and realised how much development effort it took just to keep Solr up and running. Azure AI Search was the right approach, because it removes most of that infrastructure management. Once we decided, I committed fully and led the migration, which delivered sub-second results, a more than 98% latency reduction.
  >
  > What I learned: just because a team is using a tool and it works fine doesn't mean we should stick with it. We should look at the trade-offs, how it affects the team, what the alternatives are, and what we gain by moving. This also set a standard for future migrations like this.
- **Follow-ups to prep:** How senior was the platform engineer? What were the numbers (hours a month on Solr, vulnerabilities)? What did you give up by leaving Solr? Would you decide the same today?
- **Grade:** 6/10 → 7/10 (2026-11-24): fair on both sides, settled by numbers, disagree-and-commit ending.

### D4 · A director's unrealistic deadline for the Splunk Cloud migration (Citrix, same project as F3)
- **Answers:** D4 (also E3 pushing back on a deadline, B3)
- **My answer (my words):**
  > A director set an unrealistic deadline for our Splunk migration. It was hard to meet: we were the first team moving to a new Splunk Cloud instance, so the infrastructure had to be set up first, and then the data had to be ingested. This was government compliance data, so it had to be ingested very carefully. It involved a lot of teams and millions of records across 40 different tables. Pushing data with Azure Automation was something nobody in the teams had done before, so there was a big learning curve, and given how critical the data was and the PII in it, it had to be approached carefully. Even after migrating, every team had to cross-verify that their data matched across both instances.
  >
  > So I had a discussion with the director and explained what we were dealing with: compliance data, PII, a new way to ingest data, then running tests to make sure there was no data mismatch, then each team running their tests to make sure their dashboards were up to date, then a new CI/CD process for those dashboards, and the alerts that needed to be migrated.
  >
  > After discussing the trade-offs, the director (and directors) agreed the deadline was unrealistic. I proposed a new, realistic date, and we achieved it successfully.
- **Follow-ups to prep:** What were the original and new dates? Why did the director want the earlier date (licence expiry, cost)? Did you offer options, like moving the most critical data first? (Keep the timeline consistent with F3's "about 6 weeks".)
- **Grade:** 7/10 on first attempt (2026-11-24): concrete scope explained in business terms, a new date that was met.

### D5 · "Too technical with non-technical teams" (Citrix, half-year review)
- **Answers:** D5 ⭐ (also A7 weakness, D4)
- **My answer (my words):**
  > In my half-year review, my manager told me that I sometimes get too technical when discussing things with non-technical teams.
  >
  > At first I thought I was doing it on purpose, so that I was very clear in communicating and setting expectations. Then I asked for a few examples. My manager said that during PI planning, which involves non-technical teams, I was giving too much technical information about each feature or epic, more than the other teams needed to know.
  >
  > I took it as good feedback. In the following PI planning, I only discussed scope and delivery rather than technical detail, and I've started discussing the technical details in sprint planning, design reviews, or only with the team members who will be working on it.
- **Follow-ups to prep:** How did you know it worked (proof)? How do you decide how much detail an audience needs? Do you ask for feedback regularly now?
- **Grade:** 8/10 on first attempt (2026-11-24): real feedback, honest reaction, concrete change; add one line of proof it worked.

### D6 · Feedback on a new mid-level engineer's PR (PR reviews)
- **Answers:** D6 (also E4 raising the bar, E6 mentoring)
- **My answer (my words):**
  > While reviewing PRs, I gave negative feedback to a mid-level engineer who had joined recently. They implemented a feature and raised a PR for review, but it didn't follow the coding standards the team follows, it was missing test cases, and it didn't include test plans in the work item for the QA team.
  >
  > I pointed these things out in the PR, then had a discussion with them. I pointed them to the docs we follow, how we frame test cases, and how to follow our ephemeral test plan process, and I pointed out a few mistakes and how they could be done more effectively.
  >
  > They acknowledged it, we discussed it, and they worked on all of these things. Going forward, I didn't see any of these issues in their PRs.
- **Follow-ups to prep:** Why did it matter (impact on QA, bugs)? How did they react? Was it an onboarding gap, and did you fix it for the next new joiner? What is the "ephemeral test plan process", in one line?
- **Grade:** 7/10 on first attempt (2026-11-24): specific, private follow-up, proof it worked.

### D7 · Documentation-first vs. move-fast, with a platform engineer (Citrix, VM migration)
- **Answers:** D7 (also E2, E4)
- **My answer (my words):**
  > I'm someone who likes documenting things: creating ADRs for architecture decisions and approaches, so we can always refer back to them in future work.
  >
  > During the VM migration, I was following each step carefully: documenting everything, creating ADRs and sharing them for review and approval. I like my work to be reviewed by others, and only once it's approved do I move forward with the actual implementation.
  >
  > A particular platform engineer liked to get things done faster: no need for a POC, just complete the delivery as soon as possible. I, on the other hand, like to create a POC and a working solution, document it, write an ADR, get it reviewed, write test plans and test cases, get a working prototype in staging, and create a delivery plan and a rollback plan.
  >
  > I had a discussion with this platform engineer and explained each step: how it would benefit me, the team, and others who might work on this project or implement something similar. Meeting the deadline is important, and so is following a good, established process.
  >
  > They agreed, and I continued with my process. It has been useful for people new to this project, or creating something similar.
- **Follow-ups to prep:** What did you change about *your* way of working for them? Did the process slow delivery? When is a POC and an ADR overkill?
- **Grade:** 6/10 on first attempt (2026-11-24): a real style difference, but the story shows them adapting to you, not you adapting; it's also the 3rd use of the VM migration (F2, D1).

_New story: copy this template._

### <Story title>
- **Answers:**
- **Situation:**
- **Task (my job in it):**
- **Action (what I did: "I", not "we"):**
- **Result (numbers):**
- **What I'd do differently:**
- **Follow-ups to prep:**
