# Phase 5: Behavioral, leadership and resume: start here

Phase 5 covers M29 (stories and question bank), M30 (deep dives, motivation, leveling) and M31 (resume and job search) as one set of numbered docs. Work through them **in number order**. Each section doc holds the general questions senior loops ask, the questions about my own resume that fit there, and the STAR stories that answer them (behavioral stories *are* the resume told as stories).

⭐ = asked in almost every loop.

## The order, and why
| # | Doc | Plan | ⭐ questions | Why this position |
|---|---|---|---|---|
| 001 | This page | M29 | — | The rules, the story inventory, the gaps |
| 002 | [002-growth-mistakes](002-growth-mistakes.md) | M29 · F | F1 | The failure story is the hardest to find and asked every time: find it first |
| 003 | [003-conflict-collaboration](003-conflict-collaboration.md) | M29 · D | D1, D2, D5 | The other story most people lack |
| 004 | [004-ownership-impact](004-ownership-impact.md) | M29 · B | B1, B3 | Your biggest stories (search migration, cost savings, Byte AI); most-asked section |
| 005 | [005-execution-quality](005-execution-quality.md) | M29 · G | G1, G2 | Bugs and incidents (deadlock, weekend incident, FedRAMP fixes) |
| 006 | [006-influence-leadership](006-influence-leadership.md) | M29 · E | E1 | Leading the team, standards, adoption |
| 007 | [007-ambiguity](007-ambiguity.md) | M29 · C | C1 | Mostly reuses stories from 004 |
| 008 | [008-customer-focus](008-customer-focus.md) | M29 · H | — | Short; reuses stories |
| 009 | [009-ai](009-ai.md) | M29 · I | I1, I2 | AI tools at work; ties to Phase 4 |
| 010 | [010-team-process](010-team-process.md) | M29 · K | — | Facts, not stories: quick |
| 011 | [011-openers-motivation](011-openers-motivation.md) | M29 · A + M30 | A1, A2, A3, A5, A7 | "Tell me about yourself" summarises all the stories, so write it last |
| 012 | [012-resume-deep-dive](012-resume-deep-dive.md) | M31 | — | Defend every resume line, technically |
| 013 | [013-deep-dive-azure-ai-search](013-deep-dive-azure-ai-search.md) | M30 | — | 20-minute backend deep dive |
| 014 | [014-deep-dive-byte-ai](014-deep-dive-byte-ai.md) | M30 | — | 20-minute AI deep dive |
| 015 | [015-leveling-and-leadership-signals](015-leveling-and-leadership-signals.md) | M30 | — | Make every answer sound senior/staff |
| 016 | [016-resume-rewrite](016-resume-rewrite.md) | M31 | — | Rewrite the resume to match the stories |
| 017 | [017-job-search](017-job-search.md) | M31 | — | Targets, referrals, tracker, recruiter screen |
| 018 | [018-negotiation](018-negotiation.md) | M30 | — | Needed at the screen and the offer |

**Phase 5 is done when:**
- every question in 002–011 has a rehearsed story or answer under 3 minutes (M29);
- the two deep dives survive five levels of "why", and the motivation answers are clear (M30);
- 2–3 reviewed resume variants, matching profiles, a target list with referrals requested, and the first applications sent (M31).

**Privacy:** the repo is public. Personal stories are only pushed once the repo is private.

**Rules for every story:** numbers (scope, team size, metrics, impact), "I" not "we", a "what I'd do differently", under 3 minutes, follow-ups ready ("what exactly was *your* part?", "what would you do differently?").

## Story inventory (from the resume + notes)
21 candidate stories, pre-filled with what the resume and notes say; every ❓ is yours to fill. Each lives in one doc and can answer questions in others.

| Story | Where | Lives in | Answers |
|---|---|---|---|
| ST1 · Four vulnerabilities fixed ahead of SLA during FedRAMP High | Citrix | [005-execution-quality](005-execution-quality.md) | G7, G6, B3, G4 |
| ST2 · 4 Windows VMs → Linux Kubernetes pods (~$20K/yr) | Citrix | [004-ownership-impact](004-ownership-impact.md) | B13, B8, B4 |
| ST3 · Cleanup jobs for orphaned Azure images (97% cut, ~$34K/yr combined) | Citrix | [004-ownership-impact](004-ownership-impact.md) | B12, B6, B2, B8 |
| ST4 · FedRAMP asset inventory: a ~14K-resource script, then a RegScale POC | Citrix | [004-ownership-impact](004-ownership-impact.md) | B14, B9, E2, B6 |
| ST5 · Policy-as-code and Splunk-as-code (each update ~60 → under 10 min) | Citrix | [006-influence-leadership](006-influence-leadership.md) | E9, B6, E1 |
| ST6 · Azure OpenAI vulnerability-triage tool (weekly manual → daily automated) | Citrix | [009-ai](009-ai.md) | I7, I1, I2, I6, B6 |
| ST7 · Earning credibility fast in a new org | Citrix | [006-influence-leadership](006-influence-leadership.md) | E8, E2, G2 |
| ST8 · On-prem wildcard search → Azure AI Search (>98% latency cut) | UPMC | [004-ownership-impact](004-ownership-impact.md) | B10, B1, A5, B4, H2 |
| ST9 · Solr vs. Azure AI Search: choosing the technology | UPMC | [004-ownership-impact](004-ownership-impact.md) | B9, E2, F2 |
| ST10 · Owning 10 microservices, 25 workers and 8 databases | UPMC | [004-ownership-impact](004-ownership-impact.md) | B11, G8, B1 |
| ST11 · Lead developer of a 12-person team: standards, reviews, mentoring | UPMC | [006-influence-leadership](006-influence-leadership.md) | E4, E7, E6, E5, K3 |
| ST12 · CI/CD in Azure DevOps: deploys ~8–10 min → under 2 | UPMC | [004-ownership-impact](004-ownership-impact.md) | B6, E1 |
| ST13 · Azure OpenAI at a health plan: natural-language queries | UPMC | [009-ai](009-ai.md) | I8, D4, I6 |
| ST14 · Patient/member Saga sync: ~20 min → under a minute | UPMC (notes, not on the resume) | [008-customer-focus](008-customer-focus.md) | H1, B7, B1 |
| ST15 · Weekend search-latency incident (0-character searches) | UPMC (notes) | [005-execution-quality](005-execution-quality.md) | G2, H1, G5 |
| ST16 · Consumer vs. API deadlock, found from deadlock reports | UPMC (notes) | [005-execution-quality](005-execution-quality.md) | G1, G5 |
| ST17 · MSMQ silently dropping messages → RabbitMQ + monitoring | UPMC (notes) | [005-execution-quality](005-execution-quality.md) | G5, B2, E1 |
| ST18 · WCF → Web API modernisation / refactoring a tightly coupled MVC app | UPMC (notes) | [005-execution-quality](005-execution-quality.md) | G3, E2 |
| ST19 · Byte AI: building and launching a product solo | Side project | [004-ownership-impact](004-ownership-impact.md) | B15, C1, C2, B4, F3, H3, A5 |
| ST20 · Infinity Finances: a FIRE planning app | Side project | [002-growth-mistakes](002-growth-mistakes.md) | F3, I1 |
| ST21 · A disagreement with my TrueSpend co-founder | Side project | [003-conflict-collaboration](003-conflict-collaboration.md) | D8, D2, A14 |

## Gaps: questions with no story yet
Find these first: they're asked in almost every loop, and the resume can't supply them.

| Question | What's missing | Where to look |
|---|---|---|
| F1 ⭐ | Biggest mistake or failure | Something that went wrong partly because of you, plus the change it led to. Look at: an incident you caused, a migration that slipped, a design you had to undo. |
| D1 ⭐ / D2 ⭐ | A real conflict with a peer, or with a manager / senior engineer | Not the co-founder one only: interviewers want a work example. Look at: design reviews, the coding-standards pushback (ST11), Solr vs. Azure (ST9). |
| D5 ⭐ | Critical feedback you received | A specific piece of feedback and what you changed. |
| D6 | Negative feedback you gave | Possibly from mentoring (ST11). |
| C3 | Conflicting priorities: what you dropped | Possibly the FedRAMP deadlines (ST1) or owning 43 things (ST10). |
| E3 | Pushing back on scope or a deadline |  |
| E5 | A struggling team member | Possibly from ST11. |
| F2 | A time you were wrong and changed your mind | Possibly ST9. |
| I2 ⭐ | A time AI was wrong and you caught it | Likely ST6 or ST13: find one concrete wrong output. |
| A7 ⭐ | A real, current weakness | Not 'perfectionist'. Plus what you're doing about it. |

## Resume deep dive
[012-resume-deep-dive](012-resume-deep-dive.md): technical "defend this line" questions for every resume bullet, plus consistency risks an interviewer may catch.


## L. Questions to ask your interviewers
- What's the team's biggest challenge right now? What does success look like at 6 and 12 months?
- How are technical decisions made (RFCs, ADRs, design reviews)?
- What separates senior from staff here?
- How does the team use AI tools?
- What was the last incident, and what changed after it?
- How do on-call and deployments work? How does cross-team collaboration happen?
