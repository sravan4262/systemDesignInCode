# F. Growth, mistakes, learning

They test self-awareness. A failure story with no real failure in it is the most common way to lose this signal.

⭐ = asked in almost every loop. **Story** = the story id that answers it (❓ gap = no story yet). Back to [001-start-here](001-start-here.md); technical "defend this line" questions are in [012-resume-deep-dive](012-resume-deep-dive.md).

## General questions
| # | Question | What they're testing | Story |
|---|---|---|---|
| F1 ⭐ | Your biggest mistake or failure, and what changed afterwards | Owning a real failure + a systemic change | ST22 |
| F2 | A time you were wrong and changed your mind | Intellectual humility | ST2 |
| F3 | Learning a new technology fast | Learning approach, speed | ST5 |
| F4 | How do you stay current? | Concrete habits | F4 answer |
| F5 | What separates senior from mid-level, and senior from staff? | You understand the level you're interviewing for | F5 answer |


## From my resume
| # | Question | What they're testing | Story |
|---|---|---|---|
| F6 | Electrical engineering to software: how did you learn it? | Learning ability, motivation | skipped (answer on the spot) |
| F7 | SwiftUI and React Native for side projects: how do you learn a new stack fast? | Learning approach | F7 answer (ST19) |
| F8 | Which part of the Citrix cloud / FedRAMP work was new to you, and how did you ramp up? | Ramping up in a new domain | skipped (use F3) |

## Stories
_Pre-filled from the resume and notes. Fill every ❓ in your own words; keep each story under 3 minutes (Situation + Task ~20%, Action ~60%, Result + lesson ~20%)._

### ST22 · Records missed by my migration tool (UPMC, Azure AI Search migration)
- **Answers:** F1 ⭐ (also G3, G4; part of ST8)
- **My answer (my words):**
  > During our search migration to Azure AI Search, I built the tool that migrated millions of records into the index. My mistake was in that tool: roughly a few thousand records were never migrated, and users couldn't see data they should have been seeing.
  >
  > I immediately went through the tool's logs to find which records were missing, and realized the tool had a bug where it reported the wrong numbers. One batch of records went under the radar and was never imported. It happened because of an edge case in production data. I had written test cases and done a few checks to make sure all the data was imported, but the checks relied on what the tool reported.
  >
  > Now, for any migration with a lot of data, I check the batch ranges and the keys actually imported against the source, not just the tool's count. I make sure any failure is clearly visible, so there's no silent success. I rehearse on production-like data before the real run, and I only switch users over after that check passes.
- **Follow-ups to prep:** What would you do differently? How did your manager react? Has it happened since? What did the team change, not only you?
- **Grade:** 5/10 on first attempt (2026-11-24); "what changed" now concrete.

### F2 · Windows VMs: Windows pods → Linux containers (Citrix, same project as ST2)
- **Answers:** F2 (also B13, B4; the STAR version of the project is ST2 in [004-ownership-impact](004-ownership-impact.md))
- **My answer (my words):**
  > On the Windows VM to Kubernetes migration, I proposed moving these Windows VMs to Windows-based Kubernetes pods.
  >
  > A teammate showed me that for this particular service, it could run on Linux as well, so there was no need for Windows.
  >
  > I started reviewing that evidence: I went through the VMs to see which services were running, and found Linux alternatives for the Windows-based services.
  >
  > I went with the Linux-based approach. It was cheaper, because there was no need for new Windows-based nodes.
  >
  > Now, before I propose a migration, instead of assuming that Windows to Windows is the right call, I check the services, then check the Linux alternatives, and come up with a plan to migrate without any downtime.
- **Follow-ups to prep:** Why didn't you check what was running before you proposed it? How did you tell the team? How do you know when to hold firm vs. change?
- **Grade:** 6/10 on first attempt (2026-11-24); challenge and lesson now concrete.

### F3 · Learning Azure Automation in 6 weeks: Splunk Enterprise → Splunk Cloud (Citrix, part of ST5)
- **Answers:** F3 (also F8; the resume line is ST5 in [006-influence-leadership](006-influence-leadership.md))
- **My answer (my words):**
  > Our team was migrating from Splunk Enterprise to Splunk Cloud. All the data had to be pushed into Splunk Cloud through its API, and this had to happen within our compliance boundary. Azure Automation is one tool that runs within the boundary and meets those requirements for pushing the data. I had about 6 weeks before Splunk Enterprise shut down, and all the compliance data had to be migrated and validated by then.
  >
  > So I needed to learn Azure Automation: how it works and how it runs within the boundary. I read through our company docs to find out how it runs within the boundary, and read the official docs. I used AI to speed up the research on how to build the automations, then confirmed what it told me against the docs.
  >
  > Then I built a small prototype and ran end-to-end tests to check that the data was being pushed into the Splunk Cloud index and was visible. I made sure it was reviewed by an SME in these areas, and I shipped on time.
- **Follow-ups to prep:** How does the runbook authenticate, and where is the Splunk token stored? What happens when a push fails (retries, duplicates)? How often does it run, and who notices if it stops? Why push and not pull?
- **Grade:** 7/10 on first attempt (2026-11-24); timeframe and AI verification added.

### F4 · How I stay current (habits + the AI skill at Citrix)
- **Answers:** F4 (also I1, I6, E1)
- **My answer (my words):**
  > There are a couple of blogs I follow regularly to keep myself up to date. Along with that, I follow a few people on LinkedIn who share useful insights about tech stacks and AI models: detailed analysis of what's new, and how it's better than the previous version.
  >
  > One time, I learned about AI skills from one of those posts, and it reminded me of a similar problem I had at Citrix. So I implemented it and created an AI skill. I let it run for a few days and found it quite useful, then I shared it with the team, and it became part of the repo.
- **Follow-ups to prep:** Which blogs, and who on LinkedIn? What did the skill do, and what problem did it solve? How did the team start using it? Something you learned recently that changed your opinion? A technology you're skeptical of?
- **Grade:** 7/10 on first attempt (2026-11-24): a real example that went from learning to the team's repo; name the blogs and the people, and say in one line what the skill did.

### F5 · Mid-level vs. senior vs. staff, and where I am
- **Answers:** F5 (see [015-leveling-and-leadership-signals](015-leveling-and-leadership-signals.md))
- **My answer (my words):**
  > A mid-level developer delivers a well-defined feature. A senior takes a vaguely defined problem, refines it, delivers it, and owns it end to end. A staff engineer works across different teams, provides technical direction and sets standards.
  >
  > At UPMC, I owned 10 different microservices, and I led the search migration, which had substantial results.
  >
  > At Citrix, I led and delivered the first-of-its-kind VM migration to Kubernetes, then set the CI/CD policies and the Azure Policy-as-code standards. Along with that, I created an AI skill that is used by multiple teams, and I led the migration from Splunk to Splunk Cloud.
- **Follow-ups to prep:** Which of these reached beyond your own team? What was "first of its kind" about the VM migration? Which teams use the AI skill? What would you need to do to be staff here?
- **Grade:** 7/10 on first attempt (2026-11-24): a clear model with real examples; keep the claims consistent with your other stories.

### F7 · Learning SwiftUI for Byte AI's iOS app (side project, ST19)
- **Answers:** F7 (also F3)
- **My answer (my words):**
  > I read some official docs, asked AI to speed up the research, and then cross-verified it. I first built a very small end-to-end prototype. Once I was comfortable, I started building screens slowly, one piece at a time.
  >
  > Once a piece was built, I connected my phone to my Mac and debugged it to see how it was performing. Since the backend was already built and serving the web app, I connected to my test instance of the backend and ran some tests.
  >
  > I took small steps: building multiple screens, adding tests, adding CI/CD pipelines, getting it deployed. Then I set up the Apple developer account and submitted the app for App Store approval. It was denied, and Apple asked for more. It was a very good learning curve. I have another app in progress, and I would definitely do it again.
- **Follow-ups to prep:** Why did Apple deny it, and what did you change? What surprised you about SwiftUI compared with React? How do you test the iOS app?
- **Grade:** 7/10 on first attempt (2026-11-24): a clear, incremental method with a real setback.

### ST20 · Infinity Finances: a FIRE planning app (Side project)
- **Answers:** F3, I1
- **Known (resume / notes):** Multi-calculator financial-independence app (Next.js, Hono API, Supabase): month-by-month simulation with Monte Carlo and tax-optimised withdrawal sequencing; Gemini for structured extraction of inputs from natural language.
- **Situation:** ❓ Why you built it
- **Task (my job in it):** ❓ Solo
- **Action (what I did: "I", not "we"):** ❓ What you had to learn (finance domain? Monte Carlo?) and how fast
- **Result (numbers):** Live; ❓ users
- **What I'd do differently:** ❓
- **Follow-ups to prep:** How do you validate the Gemini extraction? How did you test the simulation?

_New story: copy this template._

### <Story title>
- **Answers:**
- **Situation:**
- **Task (my job in it):**
- **Action (what I did: "I", not "we"):**
- **Result (numbers):**
- **What I'd do differently:**
- **Follow-ups to prep:**
