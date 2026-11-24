# K. How your team works (process)

Factual questions, not stories. Answer in 1–2 minutes with your team's real process; they test engineering maturity.

⭐ = asked in almost every loop. **Story** = the story id that answers it (❓ gap = no story yet). Back to [001-start-here](001-start-here.md); technical "defend this line" questions are in [012-resume-deep-dive](012-resume-deep-dive.md).

## General questions
| # | Question | What they're testing |
|---|---|---|
| K1 | Walk me through your release process (change requests, RC tags, load tests, feature flags, release-night monitoring) | Change control, safe releases |
| K3 | How does your team plan work (SAFe PI planning, grooming, capacity, dependencies)? | Planning, estimation |
| K7 | Code-review rules, testing expectations, on-call rotation, incident process | Engineering culture |

## My answers
_One short paragraph per question: the real process, plus one thing you'd improve._

### K3 · How we plan work (my words, 2026-11-24, saved as is)
> We follow the PI approach with five 2-week sprints, so about one PI every quarter. Every PI, we create features/epics from the dev side for KTLO and regular maintenance, and then there are the features from the product team.
>
> During the PI we run grooming sessions. First we run design sessions: we design the feature and plan how to implement it. Once that's done, we take the feature and break it into individual stories in grooming sessions, and those stories are groomed with appropriate acceptance criteria, test cases and effort.
>
> We have a fixed amount of working capacity for a PI, so once everything is groomed and prioritised, we start taking the highest-priority items.
- **Grade (K3):** 6.5/10. Clear and accurate: the cadence (5 × 2-week sprints ≈ a quarter), KTLO alongside product features, design session → story breakdown → acceptance criteria + test cases + effort, fixed capacity filled by priority.
- **To strengthen (❓):**
  - **Your role (the senior signal):** e.g. "I lead the design sessions for my area, break epics into stories, and flag risks and spikes."
  - **Dependencies (in the question, missing):** how cross-team dependencies are found and tracked (program board, linked tickets, agreed dates) and what you do when another team slips.
  - **Capacity detail:** how much is reserved for KTLO / on-call / unplanned work (e.g. ~20%), and that you don't plan to 100%.
  - **Changing priorities mid-PI:** what happens when something urgent lands. Your example: ST25 (two deadlines, raised with the manager → directors re-prioritised).
  - **One thing you'd improve** (the doc asks for it): e.g. a fixed KTLO/tech-debt share each PI, or tracking predictability (planned vs. delivered).

### K7 · Code review, testing, on-call, incidents (my words, 2026-11-24, saved as is)
> **Code review rules:** name the branch by type (feature, bug fix, hotfix). Create a PR; PR size should be limited to fewer files, smaller PRs are encouraged. Each PR needs at least 3 approvals. Code must follow standards, like naming conventions and how config is added.
>
> **Testing:** at least one positive and one negative test case for every piece of business logic, and at least 80% unit-test coverage. Test cases and test plans are added to the work item itself along with the PR, and the test plans also include any stress/load testing.
>
> **On-call:** every person goes on call twice a week, two 4-hour sessions a week, with a primary and a secondary. The same 2 people are primary/secondary for a 4-hour session. If the primary or secondary isn't available, they have to find a replacement for that time.
>
> **Incidents:** triaged by severity, updates to stakeholders, incident troubleshooting, an incident postmortem, and lessons learned.
- **Grade (K7):** 7/10. Concrete and specific (3 approvals, 80% coverage, positive + negative case per business rule, test plans on the work item, primary/secondary with self-arranged cover). Specific numbers make it believable.
- **To strengthen (❓):**
  - **One "why" per rule:** e.g. small PRs → reviewers actually read them; negative tests → most prod bugs are in error paths.
  - **Be ready for push-back:** "3 approvals: doesn't that slow you down?" (have an answer: e.g. turnaround norm, or 3 because of FedRAMP change control). "80% coverage: is coverage a good metric?" (it's a floor, not a goal; the positive/negative rule matters more).
  - **Incidents:** "mitigate first, root cause second", blameless postmortems, action items tracked to done; cite **ST24** (health checks added to every release request).
  - **One thing you'd improve:** e.g. config validation at startup (`ValidateOnStart`), runbooks for common alerts, a review-turnaround target.
