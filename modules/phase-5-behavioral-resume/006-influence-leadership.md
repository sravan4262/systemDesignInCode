# E. Influence and leadership

The senior-vs-staff line. They test whether people follow you without being told to.

⭐ = asked in almost every loop. **Story** = the story id that answers it (❓ gap = no story yet). Back to [001-start-here](001-start-here.md); technical "defend this line" questions are in [012-resume-deep-dive](012-resume-deep-dive.md).

## General questions
| # | Question | What they're testing | Story |
|---|---|---|---|
| E1 ⭐ | Influencing without authority; getting a team to adopt something | Evidence, building allies | ST5 + the Slack API skill (learner's pick 2026-11-24, story to tell later), ST12, ST17 |
| E4 | How you raise the bar / ensure code quality on a team | Standards, reviews, automation | Jenkins + VM migration pipelines (learner's pick 2026-11-24, story to tell later), ST11 |
| E6 | Mentoring someone; a junior stuck on a bug for hours | Growing others | ST11 (answer 6.5/10 saved 2026-11-24) |

## From my resume
| # | Question | What they're testing | Story |
|---|---|---|---|
| E8 | Citrix: mentoring and leading reviews after less than a year: how did you earn credibility quickly? | Credibility in a new org | ST7 |

## Stories
_Pre-filled from the resume and notes. Fill every ❓ in your own words; keep each story under 3 minutes (Situation + Task ~20%, Action ~60%, Result + lesson ~20%)._

### ST5 · Policy-as-code and Splunk-as-code (each update ~60 → under 10 min) (Citrix)
- **Answers:** E9, B6, E1
- **Known (resume / notes):** Azure Policy as code via Jenkins (each update ~60 min → under 10); push-based Splunk ingestion with Azure Automation runbooks; Splunk dashboards and alerts deployed as code.
- **Situation:** ❓ How policy updates were done before (manual, portal, error-prone?)
- **Task (my job in it):** ❓ Your idea? Who owned the old process?
- **Action (what I did: "I", not "we"):** ❓ What you automated first; how you got others to use it (demo? a first win?); who resisted and why
- **Result (numbers):** ~60 → under 10 min per update; ❓ how many updates per week/month; ❓ fewer mistakes?
- **What I'd do differently:** ❓
- **Follow-ups to prep:** Did anyone resist? How did you get adoption? What breaks if a bad policy ships through the pipeline?
- **My answer for B6 (my words, 2026-11-24, saved as is):**
  > I noticed a problem: applying Azure policies took a lot of manual intervention every time. The Azure policies for the gov subscriptions were stored as code, but they didn't follow a CI/CD process. To apply them inside the boundary, someone logged into one of the VMs in the boundary, pulled the repository, connected to Azure with a service account, and ran the policies. It was a very manual process, not automated.
  >
  > I suggested we automate it with pipelines. I proposed an approach, the team agreed, and I built the automation: CI/CD pipelines in Jenkins, inside the boundary, that pull the policies from the repository and apply them in Azure.
  >
  > Now when someone updates a policy, there's no manual process: they make the change and commit to the main branch, and the pipeline triggers and applies the policies. Manual intervention was removed completely, and applying a policy takes a few minutes instead of a few hours.
- **Grade (B6):** 7/10. You noticed it, proposed it, built it, and the before and after is clear.
- **To strengthen (❓):**
  - **Match the resume:** "a few hours → a few minutes" here vs. "~60 → under 10 min" on the resume. Pick one defensible pair and use it everywhere.
  - **Frequency:** how many policy updates per week or month, so the saving adds up (e.g. N × ~1 h).
  - **Safety, which interviewers always ask about:** a commit to main auto-applies to gov subscriptions. Say what guards it: PR review, a what-if or validation step, dev → prod stages, an approval gate.
  - **Credentials:** the service account's secret: where it lives now (Jenkins credentials or Key Vault), and is it better than a human logging in with it?
  - **The compliance win to mention:** every change now has an audit trail (who, what, when, through Git and the pipeline logs). That's valuable under FedRAMP.
  - **Adoption:** how you got the team to agree (a demo, a first policy shipped through it); the Splunk-as-code follow-on as proof it caught on.
  - **What you'd do differently:** one line.

### ST7 · Earning credibility fast in a new org (Citrix)
- **Answers:** E8, E2, G2
- **Known (resume / notes):** Leads architecture and design reviews, mentors engineers and leads production incident response, all in under a year at Citrix.
- **Situation:** ❓ What the team was like when you joined
- **Task (my job in it):** ❓ How you went from new joiner to leading reviews
- **Action (what I did: "I", not "we"):** ❓ The first concrete things you did that earned trust; one design review you led and what changed because of it; one incident you led
- **Result (numbers):** ❓ a concrete outcome of a review or an incident
- **What I'd do differently:** ❓
- **Follow-ups to prep:** How did you earn credibility so fast? Walk me through an incident you led, minute by minute.
- **My answer for E8 (my words, 2026-11-24, saved as is):**
  > One of the first projects I worked on was running those policies manually. In the first two months, I looked at the CI/CD process for FedRAMP and understood that this tedious process could also be automated. I proposed it, built a POC, then showed actual results. The team was impressed and started involving me in design decisions, let me take full ownership of projects, let me work on bigger projects, and I became one of the FedRAMP admins.
  >
  > I think completing the work given is very important, but at the same time providing impact and being proactive is what helps the team and builds their trust, which allows you to take more ownership and take on bigger projects.
- **Grade (E8):** 6.5/10. Strong: a concrete early win in the first two months (proposal → POC → results), and a clear trust ladder (design decisions → ownership → bigger projects → FedRAMP admin). The closing lesson is good.
- **To strengthen (❓):**
  - **Overlap with E1/B6:** the same policy-as-code win is your E1 and B6 story. Add a second trust-builder so E8 isn't a retell (ST24 incident, G7 vulnerabilities, ST23 Splunk migration).
  - **How you learned first:** one line on ramping up in a FedRAMP High environment (read the boundary docs, shadowed, asked) before proposing changes, so it doesn't sound like "new person changes everything".
  - **The resume claim:** E8 asks about *leading reviews, mentoring and incident response*; name one review you led and what changed because of it, and one person you helped.
  - **Make "FedRAMP admin" concrete:** what access and responsibility it means, and why it signals trust (privileged access inside the boundary).
  - **Numbers:** the time saving from the POC (match the resume pair).

### ST11 · Lead developer of a 12-person team: standards, reviews, mentoring (UPMC)
- **Answers:** E4, E7, E6, E5, K3
- **Known (resume / notes):** Resume: mentored junior engineers; established coding standards, code review and CI/CD best practices. Notes (not on the resume yet): lead developer of a 12-person team, PI planning.
- **Situation:** ❓ The team's state before (quality problems? inconsistent reviews?)
- **Task (my job in it):** ❓ Formal lead or informal?
- **Action (what I did: "I", not "we"):** ❓ What standards you set and how; who pushed back and how you handled it; one person you mentored and how they grew
- **Result (numbers):** ❓ a measurable change (fewer bugs? faster reviews?)
- **What I'd do differently:** ❓
- **Follow-ups to prep:** Who pushed back on the standards? How did you handle a struggling team member? How did you keep reviews from becoming a bottleneck?
- **My answer for E6 (my words, 2026-11-24, saved as is):**
  > There was a time when a junior dev was struggling to identify the root cause of a bug. They spent multiple hours trying to find it but couldn't, so they reached out to me for guidance.
  >
  > I first understood what the bug was about: a data mismatch on a particular screen. I suggested they check these places: the API request; what queries are being run against the DB; run the query in the DB and check the data. If it's an issue with the query, tweak and verify. If not, it's an issue in the code: once the SQL query returns the data to the application, check how it's being mapped, which business classes it crosses, how it's built into the response, and check whether the user has permission to view the data. Open the browser tab to see whether it's missing there, or whether it's a front-end bug, then verify the same on the front end.
  >
  > The dev tried all of these and couldn't see any issue. Then I had a discussion with them and made sure they had checked everything properly. I was puzzled, as everything seemed fine from the API.
  >
  > Then I checked the front end. The first thing I checked was the API header: the API is versioned, and this property is available in v2, not in v1. The front end was still using v1, hence it wasn't being shown. I verified what was causing it: the feature flag to call v2 was turned off in dev. We turned it on.
  >
  > So my approach is: first I check what the dev tried, then I suggest a few other things to check, and if it's still not found, that's when we debug together.
- **Grade (E6):** 6.5/10. Strong: a layered debugging method (DB → mapping → permissions → response → front end) that the junior can reuse, and a clear escalation ladder (what did you try → what to check → debug together). The root cause (v1 vs. v2 behind a feature flag) is a good, believable detail.
- **To strengthen (❓):**
  - **Who drove at the end:** right now *you* found it. Say the junior drove while you asked questions ("what version is the request calling?"), so the "aha" is theirs.
  - **The growth (the main signal, currently missing):** what changed for that person afterwards (debugged alone next time, wrote it up, helped someone else).
  - **Make it lasting:** e.g. you turned the checklist into a team debugging guide, or set a "stuck > 1 hour, ask" norm.
  - **Prevention:** log or show the API version in responses, keep feature flags in sync across environments, contract tests between front end and API.
  - **Tone:** "I ensured they checked everything properly" can sound like distrust; frame it as walking through it together.

_New story: copy this template._

### <Story title>
- **Answers:**
- **Situation:**
- **Task (my job in it):**
- **Action (what I did: "I", not "we"):**
- **Result (numbers):**
- **What I'd do differently:**
- **Follow-ups to prep:**
