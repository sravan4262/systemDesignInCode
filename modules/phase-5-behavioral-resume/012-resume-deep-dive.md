# Resume deep dive: defend every line

Interviewers pick any line and drill in: "how exactly?", "how did you measure that?", "what went wrong?". For each line: a 60-second explanation, then answers to the questions below. A line you can't defend gets cut in M31. Story ids (ST…) link to the STAR versions in the section docs; back to the [index](001-start-here.md).

**Three questions that apply to every line:**
1. What was **your** part, and what was the team's?
2. How did you **measure** the number (before and after, what tool, what load)?
3. What **went wrong**, and what would you do differently?

## Headline and summary
| Line | Questions to expect |
|---|---|
| Headline: Senior Cloud Engineer · Full-Stack · Generative AI & DevOps | Which one are you? Which role are you applying for? (Four identities read as unfocused; M31 picks one per resume variant.) |
| "Senior Software Engineer, 9+ years" | Titles differ across the resume (Senior Cloud Engineer / Senior Software Engineer / Senior Cloud Development Engineer): which is accurate? |
| Expertise: C#, .NET, Terraform, React, GenAI across Azure, AWS, GCP | Where did you use Terraform at work (it appears only on TrueSpend)? What exactly did you run on AWS and GCP? |
| Two production apps built independently + co-founded TrueSpend | How do you balance this with a full-time job? Are you planning to leave for TrueSpend? (Check IP and moonlighting policies first.) |

## Citrix: Senior Cloud Development Engineer (Oct 2025 – present)
| Line | Questions to expect |
|---|---|
| Multi-cloud infra for FedRAMP High; helped reach production-ready status | What is FedRAMP High, in one sentence? Which controls were hardest? What did *you* change? What's the difference between Moderate and High? |
| 4 vulnerabilities fixed ahead of SLA (TLS bypass, command injection, IDOR, authorization bypass) — ST1 | Explain each class and its fix generically. How do you prevent IDOR systematically (ownership checks in one place, tests)? How did you verify each fix? How did you prioritise? |
| 4 Windows VMs → Linux Kubernetes pods, ~$20K/yr — ST2 | What ran on them? How did you port Windows-only parts? Show the $20K math. Rollback plan? Why pods and not Container Apps? Resource limits, health probes? |
| TeamCity cleanup jobs: managed images $2.1K → $62/month, ~$34K/yr combined — ST3 | How do you decide an image is orphaned? How do you make deletion safe (dry run, retention, tags)? What's in the "combined" $34K? |
| Python inventory of ~14K Azure resources on Windows Task Scheduler — ST4 | Why Task Scheduler and not Azure Automation / Functions? How do you handle API throttling and partial failures? Who notices if it doesn't run? (M31: lead with the outcome, not the scheduler.) |
| Azure Policy as code via Jenkins (~60 → under 10 min), Splunk ingestion runbooks, dashboards as code — ST5 | What's slow about a manual policy update? How do you test a policy before it applies? What stops a bad policy from locking resources? Push vs. pull ingestion: why push? |
| C# + Azure OpenAI tool: classifies vulnerabilities, finds the owner, creates Jira tickets; weekly → daily — ST6 | How did you validate classifications? Accuracy? What happens on a wrong owner? Prompt design, structured output, cost per run? Duplicate tickets (idempotency)? Data sent to the model: anything sensitive? |
| Architecture and design reviews, RegScale POC (~14K resources), mentoring, incident response — ST4, ST7 | How do you run a design review? Build vs. buy for RegScale: what tipped it? An incident you led, minute by minute? |
| Tenure | Why leave after about a year? (Forward-looking, no blame.) |

## UPMC Health Plan: Senior Full-Stack .NET Web Developer (Jan 2018 – Sep 2025)
| Line | Questions to expect |
|---|---|
| Led platform initiatives through the full SDLC | Pick one and walk me through it end to end. |
| Owned 10 microservices, 25 workers, 8 databases (C#, .NET 8, ASP.NET Core) — ST10 | What does "owned" mean? Service boundaries: why 10? How do services talk? On-call? What broke worst? |
| On-prem wildcard search → Azure AI Search, >98% latency cut, load-tested — ST8 | From what to what (p50/p99)? Why was wildcard search slow (leading wildcards, no index)? Index design, analyzers? How did you keep the index in sync? How did you load-test? Why not Azure Data Factory? Vector/NLP re-index: what model? (Deep dive #1 in M30.) |
| Mentored juniors; coding standards, code review, CI/CD practices — ST11 | Who pushed back? How did you enforce standards (analyzers, PR templates)? How did you know it worked? |
| Azure OpenAI for natural-language queries and actionable workflows — ST13 | What exactly did users ask? How did you stop it generating a wrong or unsafe filter? PHI: what left your boundary? Which model, and why? |
| Azure DevOps YAML pipelines to App Service, deploys ~8–10 → under 2 min — ST12 | What made them slow? Deployment slots? Rollback? |
| Angular and React front-ends with Redux | Redux with Angular, or NgRx? (Make sure the line is accurate.) Which app used which? |
| SQL Server, Cosmos DB, Redis; NServiceBus over RabbitMQ — ST16, ST17 | Why Cosmos for some data and SQL for others? Cache invalidation? NServiceBus: outbox, retries, error queue? How did you handle duplicate messages? |
| Solr Cloud on OpenShift with Docker; multi-client SaaS microservices — ST9 | Why move from Solr to Azure AI Search? Tenant isolation in the SaaS design: shared DB, schema per tenant, or DB per tenant? |
| Tenure | ~8 years at one employer: how did your role grow? Why stay, and why leave? |

## Accenture: Full-Stack .NET Web Developer (2017)
| Line | Questions to expect |
|---|---|
| SQL Server queries and stored procedures, +75% performance | 75% of what, measured how? What was the fix (indexes, plans, parameter sniffing)? |
| WCF (SOAP) services | Would you use WCF today? What replaces it (gRPC, REST)? |
| WPF / MVVM desktop apps | Why MVVM? (Short answers; this is early career.) |
| jQuery + InRule business rules | What is InRule, and why a rules engine? |
| Tenure | Why only one year? |

## Projects
| Line | Questions to expect |
|---|---|
| Byte AI (~50 DAU), .NET 9 + Next.js + SwiftUI + Supabase, SignalR chat, APNs — ST19 | Why build it? Architecture in 2 minutes. SignalR scale-out (backplane)? What's ~50 DAU telling you? (Never inflate the number.) |
| Hybrid search: self-hosted ONNX embeddings (nomic-embed-text) + pgvector + full-text, RRF | Why self-host embeddings? pgvector index (HNSW?) and filters? Why RRF over score blending? How did you judge search quality? (Deep dive #2 in M30.) |
| "For You" feed: cosine similarity to per-user interest embeddings | How is the interest vector built and updated? Cold start? Avoiding a filter bubble? |
| Gemini tagging, quality scoring, two-layer moderation | What are the two layers? False positives? Cost per post? Prompt injection in user content? |
| Azure blue-green releases; GitHub Actions for .NET, Vercel, TestFlight | How does blue-green work with database migrations? |
| Infinity Finances: Monte Carlo, tax-optimised withdrawals, Gemini extraction — ST20 | Monte Carlo assumptions (returns, sequence risk)? How do you validate Gemini's extracted inputs? |
| TrueSpend (co-founder): React Native + .NET 9, background geolocation, Plaid, Stripe tiers, Azure OpenAI nightly insights, Container Apps + Terraform — ST21 | Battery and privacy with background location? Plaid data security? Stripe entitlements: where's the source of truth? Who owns what between the co-founders? |

## Skills and education
| Line | Questions to expect |
|---|---|
| Long skills list (HL7, Trivy, AppDynamics, OAuth/OIDC, SignalR, …) | Anything listed can be asked. Be ready to say where you used each, or cut it (M31 trims per role). |
| M.S. Electrical Engineering → software | Why the switch? (One clear sentence.) |

## Consistency risks to fix before applying (carry into M31)
- **Three different titles** (headline, summary, Citrix role): pick one per resume variant.
- **"Angular and React … with Redux":** confirm it's accurate (Angular usually uses NgRx).
- **Terraform as "expertise"** appears only on TrueSpend: add work evidence or move it down.
- **Lead developer of a 12-person team** and the **Saga sync** (ST11, ST14) are in your notes but not on the resume: add them if true, or don't claim them.
- **Plan section 10, "Claims to verify":** some note answers claim things the resume doesn't support (micro-frontends, LaunchDarkly, ARM/Bicep, Prometheus/Grafana/ELK, ZooKeeper, an OAuth server, an MSMQ → Service Bus migration, a destructive EF migration, "3× throughput", SSIS/ADF). Tell only what you did.
