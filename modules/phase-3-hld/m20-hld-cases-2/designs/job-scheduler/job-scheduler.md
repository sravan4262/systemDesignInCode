# Distributed job scheduler (50M cron jobs)

Diagram: [job-scheduler.excalidraw](job-scheduler.excalidraw), in three rows:
- **A. Normal jobs:** every path.
- **B. Hot:** 10M jobs due at the same second, plus 1M second-level jobs. Only what differs from row A, numbered 1–7 in order.
- **C. One job, step by step:** customer / services / data lanes, left to right in time, including a scheduler node crash.

Open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

## Prompt (what the interviewer says)
> "Design a scheduler service with a portal where customers can schedule 50 million cron jobs. Jobs can run at the hour level or down to the second."

## Functional requirements (agreed 2026-11-21)
1. **Portal / API:** create, update, pause and delete a job: a cron expression **with seconds** (`*/10 * * * * *` = every 10 s), a timezone, a target and a payload.
2. **Run on time:** each job fires at its scheduled time.
3. **Running = calling the job's target:** an HTTPS webhook, or a message to the customer's queue. We don't run customer code.
4. **Run history:** status per run (success, failed, retried), plus "run now".
5. **Retries:** a failed run retries with backoff, then is marked failed.

**Out of scope (say this out loud):** job chains / workflows (B after A), running customer code in containers, billing.

## Non-functional requirements
| Requirement | Target | Why it matters here |
|---|---|---|
| Scale | 50M jobs; ~1M of them second-level (every 10 s) | Can't scan a table of 50M rows every second |
| On time | p99 start within **1 s** of the scheduled time; the top-of-the-hour burst drains in **~10 s** | 10M calls can't all start in the same second (see row B) |
| No lost runs | A scheduler node dies → its jobs fire again within ~10 s | Customers depend on runs (billing jobs, reports) |
| No harmful duplicates | **At least once** delivery, with `runId = jobId:scheduledTime` as the idempotency key | Exactly once isn't possible over a network call to someone else's system |
| Fairness | One tenant with 5M jobs can't delay everyone else | Multi-tenant SaaS |
| Available | Creating jobs can be briefly down; **firing must not stop** | The scheduler is the critical part |

## Capacity estimate (high level)
**Facts:** 50M jobs, ~2 KB definition each; 1M second-level jobs every 10 s; the rest are minute/hour level; 10M jobs use `0 * * * *` (top of the hour); a webhook call takes ~200 ms; 1 day ≈ 10⁵ s.

| What | Math | Result | So… |
|---|---|---|---|
| Steady runs/s | 1M ÷ 10 s + ~49M hourly ÷ 3,600 | **~115k runs/s** | Kafka + an executor fleet, not one DB poller |
| Top of the hour | 10M due at 00:00:00 | **10M in one second** | Pre-dispatch + pre-scale (row B) |
| Scheduler memory | 50M × ~64 B (jobId + next run) | **~3 GB total**, ~100 MB per node on 32 nodes | The whole schedule fits in memory |
| Jobs storage | 50M × 2 KB | **~100 GB** | Small; Cosmos partitioned by shard |
| Executors | ~10k calls/s per pod (async, 2k in flight) | **~25 pods steady, ~120 at the hour** | Autoscale on lag + a scheduled scale-out |
| Run history | 115k/s × 10⁵ s × 200 B | **~2 TB/day** | TTL 7 days; second-level jobs keep failures + last 100 only |

**What the numbers tell you:**
- The schedule itself is small: **3 GB fits in memory**, so don't query a database every second to ask what's due.
- The hard parts are **the top-of-the-hour spike** (10M calls at once) and **run history** (billions of rows a day).

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /jobs` | `{ cron, timezone, target: { type: webhook\|queue, url }, payload, spread? }` + `Idempotency-Key` | `201 { jobId, nextRunAt }` · `400` (bad cron) · `429` (over tenant quota) | `spread: true` = a fixed random second inside the minute |
| `PUT /jobs/{id}` | same body + `If-Match: version` | `200 { nextRunAt }` · `412` (someone else changed it) | Optimistic concurrency |
| `POST /jobs/{id}/pause` · `/resume` | — | `204` | |
| `DELETE /jobs/{id}` | — | `204` | Stops future runs; runs already in flight may still finish |
| `POST /jobs/{id}/run` | — | `202 { runId }` | "Run now": goes straight to Kafka `job-runs` |
| `GET /jobs?cursor=` | — | `200 { jobs[], nextCursor }` | The tenant's jobs |
| `GET /jobs/{id}/runs?cursor=` | — | `200 { runs: [{ runId, scheduledTime, status, attempts, httpStatus }], nextCursor }` | Newest first |

**What the customer's webhook receives:** `POST <url>` with `Idempotency-Key: <jobId>:<scheduledTime>` and an `X-Signature` HMAC header (so they can verify the call came from us). `2xx` = success; `5xx`, `429` or a timeout = retry; any other `4xx` = failed (no retry).

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Store / technology |
|---|---|---|
| Job API | Create / edit / pause / delete / run now | **Jobs (Cosmos DB)**, partition key `shardId = hash(jobId) % 4096` |
| Change publisher | Turns every job change into an event | Cosmos **change feed** → Kafka `job-changes` |
| Scheduler nodes (~32) | Each **owns some shards** through a lease. Keeps every job's next run **in memory**, sorted by second. Fires what's due. | Leases + a per-shard `fired-up-to` checkpoint in **etcd** (or Azure Blob leases) |
| Kafka `job-runs` | Buffer between "it's due" and "call it" | Keyed by `jobId`; many partitions |
| Executor workers | Insert the run, rate limit, call the target, save the status, retry | Consumer group; KEDA autoscale on lag |
| Rate limiter | Per-tenant and per-target limits | **Redis** token buckets |
| Runs | Run history | **Cosmos DB**, partition key `(jobId, day)`, TTL 7 days, `UNIQUE (jobId, scheduledTime)` |
| Jobs by tenant | Portal list of a tenant's jobs | Cosmos container, partition key `tenantId`, fed by the change feed |

**The key idea: the schedule lives in memory, split into shards.**
- 50M jobs are split into **4,096 shards** by `hash(jobId)`. Each scheduler node holds a **lease** on some of the shards (32 nodes ≈ 128 shards each).
- When a node gets a shard, it loads that shard's jobs from Cosmos (a single-partition query, ~12k jobs) and works out each job's next run time.
- The node then keeps a **list of jobs per second** for the next few minutes (a "timing wheel"). Every second it takes that second's list and publishes it. Nothing scans the database.
- After firing a job, the node computes its next run **from the cron expression in memory**. There's no database write per run for the schedule itself.

**Write path (row A):**
1. **Create / edit:** Job API → Jobs (Cosmos). The change feed → Kafka `job-changes`. **Every scheduler node reads this topic** (it's low volume) and keeps only the changes for shards it owns: add, update or remove the job in memory.
2. **Fire:** every second, each node publishes the due runs to Kafka `job-runs`: `{ runId = jobId:scheduledTime, jobId, tenantId, fireAt }`. Then it advances the shard's **`fired-up-to` checkpoint** (written every second, so a crash loses at most ~1 s of progress).
3. **Execute**, in an executor:
   1. `INSERT` a run row with `UNIQUE (jobId, scheduledTime)`. If it already exists and is done, skip it (it's a repeat).
   2. Check the tenant and target rate limits in Redis. Over the limit → delay the run (retry topic), don't drop it.
   3. Call the target, with a timeout of ~30 s and the `Idempotency-Key` header.
   4. Save the status. A failure goes to `job-retries` (10 s → 1 min → 10 min); after the last try → `failed`.
4. **Run now:** Job API publishes straight to `job-runs` with `runId = jobId:now`.

**Read path:**
- **Job list:** Job API → the jobs-by-tenant view.
- **Run history:** Run API → Runs, partition `(jobId, day)`, newest first.

## Hot cases (row B): step by step
**10M jobs due at 00:00:00:**

| # | What happens | Why |
|---|---|---|
| 1 | The next minute's due list is **already in memory** on each scheduler node | Nothing has to be loaded at the last moment |
| 2 | At **23:59:30**, publish all 10M runs **early**, each with `fireAt = 00:00:00`, **interleaved by tenant** (round-robin) | Takes the scheduler and Kafka out of the critical second; a tenant with 5M jobs can't sit in front of small tenants |
| 3 | Kafka `job-runs` holds them (more partitions, e.g. 512, for parallel executors) | Buffer |
| 4 | Executors are **scaled out at 23:59** by a KEDA cron rule (20 → 120 pods); they prefetch and **wait until `fireAt`** | The spike is **predictable**, so scale before it, not after the lag shows up |
| 5 | At 00:00:00, for each run: is the job still active (a definition cache catches jobs deleted or paused after the early publish)? Then the tenant rate limit | Correctness after pre-dispatch |
| 6 | Call the targets; the 10M calls drain in **~10 s** | No system starts 10M HTTP calls in one second; promise a drain time, not "all at :00" |
| 7 | Runs saved in **batches** | 1M row writes per second one by one would be too expensive |

**The design fix that helps most: spread.** Most "every hour" jobs don't care whether they run at :00:00 or :00:37. A `spread` option gives the job a **fixed random second** inside its minute (like Jenkins' `H` in cron). Make it the default in the portal, and the spike becomes smooth.

**1M second-level jobs (every 10 s ≈ 100k runs/s):**
- They **stay in scheduler memory**. The next run is computed there, and the Jobs DB is never read per run.
- Executors cache job definitions (target, payload), so there's no Cosmos read per run either.
- **Run history:** writing 100k rows/s forever is the real cost. For second-level jobs, keep **failures and the last 100 runs** per job, plus counters (success/fail per minute) for the dashboard.

## Life of one job + a node crash (row C)
| # | Customer | Service | Data |
|---|---|---|---|
| 1 | Creates a job: every 10 s, webhook | APIM → Job API `POST /jobs` | Jobs (Cosmos), shard 17 = `hash(jobId) % 4096` |
| 2 | — | Change feed → Kafka `job-changes` | Every node reads it |
| 3 | — | Node B owns shard 17: adds the job, next run 10:00:10 | In memory |
| 4 | — | At 10:00:10 node B publishes run `jobId:10:00:10` | Kafka `job-runs` |
| 5 | — | Executor: `INSERT` run (UNIQUE) → rate limit | Runs: `running` |
| 6 | Webhook gets `POST` + `Idempotency-Key` → `200` | Executor calls the target, saves `success` | Runs: `success` |
| 7 | Sees the run in history | APIM → Run API `GET /jobs/{id}/runs` | Runs |
| 8 | — | **Node B crashes** and stops renewing its lease | The shard 17 lease expires (10 s) |
| 9 | — | **Node C takes shard 17**, loads its jobs, restarts from the `fired-up-to` checkpoint, re-fires the missed runs | Runs UNIQUE drops any repeats |

**Why a repeat is harmless:** node B may have published 10:00:20 and died before saving its checkpoint, so node C publishes 10:00:20 again. The executor's `INSERT` hits `UNIQUE (jobId, scheduledTime)` and skips it. If the first copy was still in flight, the customer's endpoint sees the same `Idempotency-Key` twice and ignores the second. That's **at least once + idempotency**, not exactly once.

## Which components each requirement touches
| Requirement | Path | Normal (row A) | Hot (row B) |
|---|---|---|---|
| Create / edit / pause | Write | Job API → Jobs (Cosmos) → change feed → `job-changes` → owner node's memory | Same |
| Run on time | Write | Owner node (in memory, per second) → `job-runs` → executors | **Pre-dispatch** at :59:30 with `fireAt`; executors pre-scaled and waiting |
| Call the target | Write | Executor: INSERT run → rate limit → call → status | Same, plus a "still active?" check; drain over ~10 s |
| Retries | Write | `job-retries` with backoff → `failed` | Same |
| Run history | Read | Run API → Runs `(jobId, day)` | Second-level jobs: failures + last 100 + counters |
| Job list | Read | Job API → jobs-by-tenant view | Same |

## Deep dives to drive (learner leads, interviewer pushes)
1. **Why not a `next_run_at` index in SQL, polled every second?** `SELECT ... WHERE next_run_at <= now()` over 50M rows, plus an `UPDATE` for every run, means 115k writes/s just for scheduling, and a hot index at the top of the hour. That's fine for 100k jobs and wrong for 50M. In memory + shards removes both.
2. **Lease and split brain:** node B is paused by a long GC, its lease expires, node C takes shard 17, then B wakes up and fires too. Use a **fencing token**: the lease version goes on every published run, and executors (or a check in Runs) reject runs from an older lease version. UNIQUE in Runs is the final guard anyway.
3. **Misfire policy:** the whole system was down for 20 minutes. Do you fire 120 missed runs of an every-10-seconds job? Let each job choose: **fire once now** (the usual default), **fire all missed**, or **skip**.
4. **Timezones and DST:** "every day at 02:30 Europe/London" doesn't exist on the day the clocks go forward, and happens twice when they go back. Store the timezone with the job, compute in that timezone, and pick a rule (run once).
5. **Clock accuracy:** NTP on every node. Firing uses the node's clock, so a 2 s clock skew means 2 s late or early runs. Monitor skew.
6. **A slow or dead customer endpoint:** a 30 s timeout × many runs ties up executors. Use per-target concurrency limits, and a **circuit breaker per target**: after many failures, pause calls to it briefly and mark runs `failed: target unavailable`.
7. **Rebalancing:** a new scheduler node joins and takes shards from others. The hand-off uses the same path as a crash: the old owner stops and releases, the new owner loads and resumes from the checkpoint.

## Interview follow-ups (answer these after the design)
- A customer deletes a job at 23:59:45, but its 00:00:00 run was already pre-dispatched. What stops it from running?
- Node B published a run, then crashed before saving its checkpoint. Walk through what happens to that run.
- Why is at-least-once the honest guarantee here, and what does the customer have to do?
- How would you add "job B runs after job A succeeds"? (Hint: that's a workflow engine, M18 / Durable Functions, not a cron scheduler.)
