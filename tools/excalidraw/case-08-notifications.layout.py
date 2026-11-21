# Case 8: Notification system. Rows: A = transactional (every path), B = broadcast campaign (only what differs; numbers = order).
# Requirements: send via push / SMS / email / in-app, preferences + quiet hours, templates, reliable delivery + status, rate limits.
BW, H = 240, 70
PRV = '#e599f7'

Note('title', 0, -70, "Case 8: Notification system - caller -> APIM -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Purple = external provider   Grey = same as row A", 16, NOTE)

# =================== ROW A: TRANSACTIONAL ===================
T = 0
Frame('fA', 0, T, 2680, 1050)
Note('At', 20, T + 12, "A. TRANSACTIONAL (one user, one event: 'order shipped', one-time password; must arrive in seconds)", 24, W)
w1, w2, w3, w4, w5, w6 = T + 90, T + 210, T + 330, T + 450, T + 570, T + 690
r1, r2 = T + 840, T + 950
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 805, "READ PATH", 18, R)

Box('Asv', Col(0), T + 330, BW, 80, "Other services\n(orders, auth / OTP,\nmarketing)", EDGE)
Box('Acb', Col(0), w5, BW, H, "Providers'\ndelivery callbacks", PRV)
Box('Aapp', Col(0), w6 + 40, BW, H, "User app\n(preferences, inbox)", EDGE)
Box('Ag', Col(1), w1, BW, r2 + H - w1, "APIM\n\nservices: mTLS +\nper-caller quota\n\nproviders: signature\n\nusers: JWT", EDGE)
Arrow('Aa1', 'Asv', 'Ag', "HTTPS", None, False, True)
Arrow('Aa2', 'Acb', 'Ag', "", None, False, True)
Arrow('Aa3', 'Aapp', 'Ag', "", None, False, True)

# write: accept -> one Kafka topic -> router -> channel topics -> workers -> providers
Box('Aapi', Col(2), w1, BW, H, "Notification API\nPOST /notifications\n+ Idempotency-Key", SVC)
Box('Adb', Col(3), w1, BW, H, "Notifications (SQL)\nkey UNIQUE\n+ outbox", DATA)
Box('Akp', Col(4), w1, BW, w2 + H - w1, "Kafka topic:\nnotifications\n\n(transactional +\ncampaign batches)", ASYNC)
Box('Art', Col(5), w1, BW, H, "Router\n1 prefs + quiet hours\n2 caps  3 render", ASYNC)
Box('Apr', Col(5), w2, BW, H, "Prefs, devices,\ntemplates (Redis\ncache -> SQL)", DATA)
Box('Akc', Col(6), w1, BW, w4 + H - w1, "Kafka topics\nby channel\n\npush\nsms\nemail\nin-app", ASYNC)
Box('Awp', Col(7), w1, BW, H, "Push worker\nretry + backoff", ASYNC)
Box('Aws', Col(7), w2, BW, H, "SMS worker\nretry + backoff", ASYNC)
Box('Awe', Col(7), w3, BW, H, "Email worker\nretry + backoff", ASYNC)
Box('Awi', Col(7), w4, BW, H, "In-app worker", ASYNC)
Box('App', Col(8), w1, BW, H, "APNs / FCM\n(token per device)", PRV)
Box('Aps', Col(8), w2, BW, H, "Twilio (SMS)", PRV)
Box('Ape', Col(8), w3, BW, H, "SendGrid / ACS\n(email)", PRV)
Box('Api', Col(8), w4, BW, H, "Inbox (Cosmos, pk\nuserId) + WebSocket\npush if online", DATA)
Arrow('Aw1', 'Ag', 'Aapi', '', W, False, True)
Arrow('Aw2', 'Aapi', 'Adb', "insert", W)
Arrow('Aw3', 'Adb', 'Akp', "outbox", W, True)
Arrow('Aw4', 'Akp', 'Art', "consume", W, True)
Arrow('Aw5', 'Art', 'Apr', "read", W)
Arrow('Aw6', 'Art', 'Akc', "", W, True, True)
for wk, pv in [('Awp', 'App'), ('Aws', 'Aps'), ('Awe', 'Ape'), ('Awi', 'Api')]:
    Arrow(f'Aw-{wk}', 'Akc', wk, '', W, True, True)
    Arrow(f'Aw-{pv}', wk, pv, '', W, False, True)

# write: delivery receipts, preferences / devices
Box('Acbh', Col(2), w5, BW, H, "Callback Handler\nPOST /callbacks/{p}", SVC)
Box('Adl', Col(3), w5, BW, H, "Deliveries (Cosmos)\n(notif, channel) UNIQUE\nsent -> delivered", DATA)
Box('Apfs', Col(2), w6, BW, H, "Preference Service\nPUT /preferences\nPOST /devices", SVC)
Box('Apfd', Col(3), w6, BW, H, "Prefs + devices (SQL)\n-> invalidate cache", DATA)
Arrow('Aw7', 'Ag', 'Acbh', '', W, False, True)
Arrow('Aw8', 'Acbh', 'Adl', "update", W)
Arrow('Aw9', 'Ag', 'Apfs', '', W, False, True)
Arrow('Aw10', 'Apfs', 'Apfd', "write", W)

# read
Box('Ast', Col(2), r1, BW, H, "Notification API\nGET /notifications/{id}", SVC)
Box('Adl2', Col(3), r1, BW, H, "Deliveries (Cosmos)\nstatus per channel", DATA)
Box('Ain', Col(2), r2, BW, H, "Inbox Service\nGET /inbox?cursor=", SVC)
Box('Ain2', Col(3), r2, BW, H, "Inbox (Cosmos)\npk userId, newest first", DATA)
Arrow('Ar1', 'Ag', 'Ast', '', R, False, True)
Arrow('Ar2', 'Ast', 'Adl2', "", R)
Arrow('Ar3', 'Ag', 'Ain', '', R, False, True)
Arrow('Ar4', 'Ain', 'Ain2', "", R)

Note('Asum', Col(4) + 20, w5 + 10, "WRITE: accept once (key UNIQUE) -> Kafka 'notifications' -> router:\n1 preferences, opt-outs, quiet hours (defer marketing)\n2 frequency caps (e.g. 5 marketing pushes / user / day)\n3 render the template per channel + language\n-> Kafka by CHANNEL -> one worker pool per channel -> provider.\nEach worker: provider rate limit, retries with backoff, dead-letter\nafter N tries; Deliveries (notif, channel) UNIQUE stops duplicates.\n\nREAD: status per channel from Deliveries; in-app inbox by userId.", 16, NOTE)

# =================== ROW B: BROADCAST CAMPAIGN (numbers = order) ===================
T = 1130
Frame('fB', 0, T, 2680, 720)
Note('Bt', 20, T + 12, "B. BROADCAST CAMPAIGN ('50% off today' to 50M users) - numbers show the order", 24, RED)
w1, w2, w3 = T + 90, T + 210, T + 330
r1 = T + 610
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 575, "READ PATH", 18, R)

Box('Bmk', Col(0), T + 280, BW, 80, "Marketing tool\n(campaign:\n50M users)", EDGE)
Box('Bg', Col(1), w1, BW, r1 + H - w1, "APIM\n\nmTLS + auth,\nquota per caller", EDGE)
Arrow('Ba1', 'Bmk', 'Bg', "HTTPS", None, False, True)

# write 1-4: split the audience into ranges; workers read only their range (no full export)
Box('Bcs', Col(2), w1, BW, H, "Campaign Service\n1 POST /campaigns\n-> 202", SVC)
Box('Bcd', Col(3), w1, BW, H, "Campaigns (SQL)\nstatus (kill switch)\n+ status per range", DATA)
Box('Bpr', Col(4), w1, BW, H, "2 Producer: ask the\nwarehouse for 1,000\nequal-size ranges", ASYNC)
Box('Bkt', Col(5), w1, BW, H, "3 Kafka topic:\ncampaign-tasks\n(1,000 tiny messages)", ASYNC)
Box('Btw', Col(6), w1, BW, H, "4 Consumer group:\ntask workers: page\nmy range, 1k at a time", ASYNC)
Box('Bseg', Col(5), w2, BW, H, "Data warehouse\nsegment members\n(read-only copy)", DATA)
Arrow('Bw1', 'Bg', 'Bcs', '', W, False, True)
Arrow('Bw2', 'Bcs', 'Bcd', "insert", W)
Arrow('Bw3', 'Bcd', 'Bpr', "start", W, True)
Arrow('Bw4', 'Bpr', 'Bkt', "produce", W, True)
Arrow('Bw5', 'Btw', 'Bseg', "my range", W)
Arrow('Bw4b', 'Bpr', 'Bseg', "ranges", W)
Arrow('Bw5b', 'Bkt', 'Btw', "consume", W, True)

# write 5-8: 1k-user batches on the same topic -> router -> throttle -> channel workers
Box('Bk', Col(6), w2, BW, H, "5 Kafka topic:\nnotifications\n(1k-user batches)", ASYNC)
Box('Brt', Col(7), w2, BW, H, "6 Consumer group:\nrouter (as row A)\nprefs, caps, kill switch", ASYNC)
Box('Bth', Col(8), w2, BW, H, "7 Campaign throttle\n~30k sends/s +\nprovider buckets", DATA)
Box('Bwk', Col(8), w3, BW, H, "8 Consumer group:\npush / email workers\n(as row A)", ASYNC)
Box('Bdd', Col(7), w3, BW, H, "Deliveries:\n(campaign, user)\nUNIQUE -> no dupes", DATA)
Arrow('Bw6', 'Btw', 'Bk', "batches", W, True)
Arrow('Bw7', 'Bk', 'Brt', "consume", W, True)
Arrow('Bw8', 'Brt', 'Bth', "token?", W)
Arrow('Bw9', 'Bth', 'Bwk', "send", W)
Arrow('Bw9b', 'Bwk', 'Bdd', "record", W)

Box('Bsame', Col(2), w3, BW, H, "Transactional sends:\nsame as row A", GREY)
Arrow('Bw10', 'Bg', 'Bsame', '', W, False, True)

# read: campaign progress
Box('Brd', Col(2), r1, BW, H, "Campaign Service\nGET /campaigns/{id}", SVC)
Box('Brc', Col(3), r1, BW, H, "Progress counters\n(Redis: sent, failed,\nskipped by prefs)", DATA)
Arrow('Br1', 'Bg', 'Brd', '', R, False, True)
Arrow('Br2', 'Brd', 'Brc', "", R)

Note('Bnote', Col(2), w3 + 110, "ONE TOPIC FOR EVERYTHING (no priority lanes): campaign batches and transactional sends share the\n'notifications' topic and the same workers. The campaign throttle (~30k/s) keeps the campaign from taking all\nthe capacity; trade-off: an OTP sent mid-campaign can wait behind batches already queued (seconds with the cap).\nThe kill switch (campaign status = cancelled) is checked per batch: stop within seconds.", 16, NOTE)
