# M20: Distributed job scheduler (50M cron jobs, second-level). Rows: A = normal jobs (every path),
# B = top-of-the-hour spike + second-level jobs (only what differs; numbers = order), C = one job's life + a Scheduler Service copy crashes.
BW, H = 240, 70
PRV = '#e599f7'

Note('title', 0, -70, "Job scheduler (50M cron jobs) - portal -> APIM -> WRITE path vs. READ path; the clock drives the scheduler", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Purple = customer's system   Grey = same as row A / failure", 16, NOTE)

# =================== ROW A: NORMAL JOBS ===================
T = 0
Frame('fA', 0, T, 2640, 830)
Note('At', 20, T + 12, "A. NORMAL JOBS (minute / hour crons: ~15-20k runs/s spread over the day)", 24, W)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2 = T + 610, T + 720
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 575, "READ PATH", 18, R)

Box('Ap', Col(0), T + 330, BW, 80, "Portal (web)\n+ customer API\nclients", EDGE)
Box('Ag', Col(1), w1, BW, r2 + H - w1, "APIM\n\nauth (JWT / API key),\nquota per tenant\n(max jobs, calls/s),\nroute by path", EDGE)
Arrow('Aa1', 'Ap', 'Ag', "HTTPS", None, False, True)

# write: save the job -> change feed -> owner scheduler -> due -> Kafka -> executors -> target
Box('Aapi', Col(2), w1, BW, H, "Job API\nPOST/PUT/DELETE /jobs\n+ pause, run now", SVC)
Box('Ajdb', Col(3), w1, BW, H, "Jobs (Cosmos)\npk shardId =\nhash(jobId) % 4096", DATA)
Box('Ajc', Col(4), w1, BW, H, "Kafka topic:\njob-changes\n(from change feed)", ASYNC)
Box('Asch', Col(5), w1, BW, w3 + H - w1, "Scheduler Service\n(32 copies, each owns\nsome shards: lease)\n\nIN: job-changes ->\nupdate the timetable\n\nEVERY SECOND:\n1 apply changes\n2 publish due jobs\n3 schedule next run\n4 checkpoint + renew", ASYNC)
Box('Alease', Col(5), w4, BW, H, "Shard leases (etcd)\n+ fired-up-to\ncheckpoint per shard", DATA)
Box('Akr', Col(6), w2, BW, H, "Kafka topic:\njob-runs\nkey = jobId", ASYNC)
Box('Aex', Col(7), w2, BW, H, "Executor workers\n(consumer group)\nautoscale on lag", ASYNC)
Box('Arl', Col(7), w3, BW, H, "Rate limit (Redis)\nper tenant + target", DATA)
Box('Atg', Col(8), w1, BW, H, "Customer target\nwebhook (HMAC signed)\nor queue", PRV)
Box('Aruns', Col(8), w2, BW, H, "Runs (Cosmos)\nUNIQUE (jobId,\nscheduledTime)", DATA)
Box('Aretry', Col(8), w3, BW, H, "Kafka: job-retries\nbackoff 10 s, 1 m,\n10 m -> failed", ASYNC)
Arrow('Aw1', 'Ag', 'Aapi', '', W, False, True)
Arrow('Aw2', 'Aapi', 'Ajdb', "write", W)
Arrow('Aw3', 'Ajdb', 'Ajc', "feed", W, True)
Arrow('Aw4', 'Ajc', 'Asch', "read", W, True, True)
Arrow('Aw5', 'Asch', 'Akr', "due", W, True, True)
Arrow('Aw6', 'Akr', 'Aex', "consume", W, True)
Arrow('Aw7', 'Aex', 'Atg', "3 call", W)
Arrow('Aw8', 'Aex', 'Aruns', "1, 4", W)
Arrow('Aw9', 'Aex', 'Arl', "2 check", W)
Arrow('Aw10', 'Aex', 'Aretry', "failed", RED, True)
Arrow('Aw11', 'Asch', 'Alease', "renew", W)

# read
Box('Alist', Col(2), r1, BW, H, "Job API\nGET /jobs?cursor=", SVC)
Box('Ajv', Col(3), r1, BW, H, "Jobs by tenant (view)\npk tenantId,\nfed by change feed", DATA)
Box('Ahist', Col(2), r2, BW, H, "Run API\nGET /jobs/{id}/runs", SVC)
Box('Aruns2', Col(3), r2, BW, H, "Runs (Cosmos)\npk (jobId, day)\nTTL 7 days", DATA)
Arrow('Ar1', 'Ag', 'Alist', '', R, False, True)
Arrow('Ar2', 'Alist', 'Ajv', "", R)
Arrow('Ar3', 'Ag', 'Ahist', '', R, False, True)
Arrow('Ar4', 'Ahist', 'Aruns2', "", R)

Note('Asum', Col(4) + 20, r1 - 30, "WRITE: Job API saves the job (Cosmos, pk = shard) -> change feed -> Kafka job-changes\n-> the Scheduler Service copy that OWNS that shard adds it to its in-memory timetable.\nEvery second the owner publishes each job due now to Kafka job-runs, then works out the next run\nIN MEMORY (no DB write per run). Executors: 1 INSERT run, UNIQUE (jobId, scheduledTime)\nstops duplicates -> 2 tenant rate limit -> 3 call the target with Idempotency-Key = runId\n-> 4 save the status; failures -> job-retries with backoff, then 'failed'.\nREAD: job list from a by-tenant view; run history from Runs.", 16, NOTE)

# =================== ROW B: TOP OF THE HOUR + SECOND-LEVEL JOBS (numbers = order) ===================
T = 910
Frame('fB', 0, T, 2640, 480)
Note('Bt', 20, T + 12, "B. HOT: 10M jobs due at 00:00:00 (\"0 * * * *\") + 1M second-level jobs - numbers show the order", 24, RED)
w1, w2, w3 = T + 90, T + 210, T + 330

Box('Bclk', Col(0), w1, BW, H, "Clock: 23:59:30\n(30 s before\nthe hour)", EDGE)
Box('Bsame', Col(0), w3, BW, H, "Create / edit jobs,\nrun history:\nsame as row A", GREY)
Box('Bsch', Col(1), w1, BW, w3 + H - w1, "1 Scheduler Service\n\nnext minute already\nin memory:\n10M jobs due\nat 00:00:00\n\n+ 1M second-level\njobs (every 10 s)", ASYNC)
Arrow('Ba1', 'Bclk', 'Bsch', "tick", None, False, True)

# 2-7: publish early with fireAt, executors pre-scaled and waiting
Box('Bpub', Col(2), w1, BW, H, "2 Pre-dispatch\nfireAt = 00:00:00,\nround-robin by tenant", ASYNC)
Box('Bk', Col(3), w1, BW, H, "3 Kafka topic:\njob-runs\n(512 partitions)", ASYNC)
Box('Bex', Col(4), w1, BW, H, "4 Executors\nprefetch, wait\nuntil fireAt", ASYNC)
Box('Bchk', Col(5), w1, BW, H, "5 Job still active?\n(definition cache)\n+ tenant rate limit", ASYNC)
Box('Btg', Col(6), w1, BW, H, "6 Customer targets\n10M calls\nspread over ~10 s", PRV)
Box('Bscale', Col(3), w2, BW, H, "KEDA cron rule:\nexecutors 20 -> 120\npods at 23:59", ASYNC)
Box('Bruns', Col(4), w2, BW, H, "7 Runs (Cosmos)\nbatched inserts", DATA)
Arrow('Bw1', 'Bsch', 'Bpub', "", W, False, True)
Arrow('Bw2', 'Bpub', 'Bk', "early", W, True)
Arrow('Bw3', 'Bk', 'Bex', "fetch", W, True)
Arrow('Bw4', 'Bex', 'Bchk', "at :00", W)
Arrow('Bw5', 'Bchk', 'Btg', "call", W)
Arrow('Bw6', 'Bex', 'Bruns', "batch", W)
Arrow('Bw7', 'Bscale', 'Bex', "scale", W, True)

# second-level jobs: never touch the Jobs DB per run
Box('Bsec', Col(2), w3, BW, H, "Second-level jobs:\nstay in memory, next\nrun computed there", ASYNC)
Box('Bsec2', Col(3), w3, BW, H, "Publish to job-runs\nevery 10 s per job\n(~100k runs/s)", ASYNC)
Box('Bsec3', Col(4), w3, BW, H, "Runs: keep failures\n+ last 100 per job\n(not every run)", DATA)
Arrow('Bw8', 'Bsch', 'Bsec', "", W, False, True)
Arrow('Bw9', 'Bsec', 'Bsec2', "", W, True)
Arrow('Bw10', 'Bsec2', 'Bsec3', "", W)

Note('Bnote', Col(5), w2, "ORDER: 1 the next minute is already in memory -> 2 at 23:59:30 publish all\n10M runs EARLY with fireAt = 00:00:00, interleaved by tenant -> 3 Kafka holds\nthem -> 4 executors (scaled out at 23:59: the spike is predictable) prefetch\nand wait -> at 00:00:00: 5 skip deleted / paused jobs, apply the tenant limit\n-> 6 call -> 7 save runs in batches.\nWHY: 10M calls can't all start in one second, so the burst drains in ~10 s,\nfairly across tenants. Jobs that don't need :00 exactly can pick 'spread'\n(a fixed random second in the minute), which flattens the spike.", 16, NOTE)

# =================== ROW C: ONE JOB'S LIFE + A SCHEDULER SERVICE COPY CRASHES (left to right in time) ===================
T = 1470
Frame('fC', 0, T, 2640, 660)
Note('Ct', 20, T + 12, "C. ONE JOB, STEP BY STEP (every 10 s, webhook) + Scheduler Service B crashes - left to right in time", 24, RED)
yU, yS, yD = T + 90, T + 250, T + 420
Note('CuL', 40, T + 58, "CUSTOMER", 18, RED)


def X(i):
    return 40 + 260 * i


CW = 220
Note('CsL', X(3), T + 218, "SERVICES", 18, W)
Note('CdL', X(2), T + 388, "DATA", 18, '#2b8a3e')

steps = [
    # (customer text, service text, service colour, data text)
    ("1 Creates a job:\nevery 10 s, webhook", "APIM -> Job API\nPOST /jobs", SVC, "Jobs (Cosmos)\nshard 17 =\nhash(jobId) % 4096"),
    (None, "2 Change feed ->\nKafka job-changes", ASYNC, "job-changes\n(every Scheduler\nService copy reads it)"),
    (None, "3 Scheduler Service B\nowns shard 17: adds\nthe job, next 10:00:10", ASYNC, None),
    (None, "4 At 10:00:10\nScheduler Service B\npublishes the run", ASYNC, "Kafka job-runs"),
    (None, "5 Executor:\nINSERT run (UNIQUE)\n-> rate limit", ASYNC, "Runs (Cosmos)\nstatus: running"),
    ("6 Webhook gets POST\n+ Idempotency-Key\n-> 200 OK", "Executor calls\nthe target,\nsaves 'success'", ASYNC, None),
    ("7 Sees the run in\nhistory: success", "APIM -> Run API\nGET /jobs/{id}/runs", SVC, "Runs (Cosmos)\npk (jobId, day)"),
    (None, "8 Scheduler Service B\ncrashes: stops\nrenewing its lease", GREY, "Shard leases (etcd)\nshard 17 lease\nexpires (10 s)"),
    (None, "9 Scheduler Service C\ntakes shard 17, loads\nit, resumes from\nfired-up-to", ASYNC, "Re-fires missed runs;\nRuns UNIQUE drops\nany repeats"),
]
# arrow between customer and service: ('down' = customer -> service, 'up' = service -> customer, label)
ulinks = [('down', "POST"), None, None, None, None, ('up', "call"), ('down', "GET"), None, None]
dlinks = ["insert", "publish", "", "publish", "insert", "", "read", "no renew", "read"]

for i, (ut, st, sc, dt) in enumerate(steps):
    Box(f'Cs{i}', X(i), yS, CW, 90, st, sc)
    if ut:
        Box(f'Cu{i}', X(i), yU, CW, H, ut, EDGE)
    if dt:
        Box(f'Cd{i}', X(i), yD, CW, H, dt, DATA)
        Arrow(f'Cad{i}', f'Cs{i}', f'Cd{i}', dlinks[i], W)
    if ulinks[i]:
        d, lab = ulinks[i]
        if d == 'down':
            Arrow(f'Cau{i}', f'Cu{i}', f'Cs{i}', lab, W)
        else:
            Arrow(f'Cau{i}', f'Cs{i}', f'Cu{i}', lab, R)
    if i:
        Arrow(f'Cn{i}', f'Cs{i-1}', f'Cs{i}', '', NOTE, True)

Note('Cnote', 40, T + 530, "Steps 1-7: one job's life. Steps 8-9: leases are short (10 s) and renewed every 3 s. When Scheduler Service B dies, the other copies take its shards, reload them from Jobs\nand re-fire every run after the shard's fired-up-to checkpoint. A run can be sent twice (B may have published it just before dying), so the Runs\nUNIQUE (jobId, scheduledTime) check and the Idempotency-Key header make a repeat harmless: delivery is AT LEAST ONCE, not exactly once.", 16, NOTE)
