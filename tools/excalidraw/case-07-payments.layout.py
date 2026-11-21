# Case 7: Payment system (a store's Payment Service in front of a card processor).
# Rows: A = normal checkout (every path), B = flash sale (same layout, only what differs; numbers = order). Run with excal.py.
# Requirements: charge (authorize + capture), never double charge, refund, daily reconciliation.
BW, H = 240, 70
PSP = '#e599f7'

Note('title', 0, -70, "Case 7: Payment system - caller -> APIM -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Purple = card processor (external)   Grey = same as row A   Red = failure path", 16, NOTE)

# =================== ROW A: NORMAL CHECKOUT ===================
T = 0
Frame('fA', 0, T, 2680, 1050)
Note('At', 20, T + 12, "A. NORMAL CHECKOUT (~300 payments/s at peak; one order -> one payment)", 24, W)
w1, w2, w3, w4, w5, w6 = T + 90, T + 210, T + 330, T + 450, T + 570, T + 690
r1, r2 = T + 840, T + 950
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 805, "READ PATH", 18, R)

Box('Aos', Col(0), T + 330, BW, 80, "Order Service\n(caller: checkout,\nrefund requests)", EDGE)
Box('Awh', Col(0), w4, BW, H, "Card processor\nwebhooks", PSP)
Box('Ag', Col(1), w1, BW, r2 + H - w1, "APIM\n\ninternal callers:\nmTLS + auth\n\nprocessor webhooks:\nIP allow-list +\nsignature check\n\nrate limit", EDGE)
Arrow('Aa1', 'Aos', 'Ag', "HTTPS", None, False, True)
Arrow('Aa2', 'Awh', 'Ag', "events", None, False, True)

# write: charge (idempotency key -> one row -> processor with OUR id as its key)
Box('Aps', Col(2), w1, BW, H, "Payment Service\nPOST /payments\n+ Idempotency-Key", SVC)
Box('Adb', Col(3), w1, BW, H, "Payments (SQL)\n1 insert: key UNIQUE\nstatus = created", DATA)
Box('Apsp', Col(4), w1, BW, H, "Card processor\n2 authorize (+capture)\nkey = paymentId", PSP)
Box('Adb2', Col(5), w1, BW, H, "Payments (SQL)\n3 authorized / failed\n+ history + outbox", DATA)
Box('Ak', Col(6), w1, BW, H, "Kafka topic:\npayment-events", ASYNC)
Box('Aoc', Col(7), w1, BW, H, "Order Service\n(consumer)", EDGE)
Arrow('Aw1', 'Ag', 'Aps', '', W, False, True)
Arrow('Aw2', 'Aps', 'Adb', "1", W)
Arrow('Aw3', 'Adb', 'Apsp', "2", W)
Arrow('Aw4', 'Apsp', 'Adb2', "3", W)
Arrow('Aw5', 'Adb2', 'Ak', "outbox", W, True)
Arrow('Aw6', 'Ak', 'Aoc', "consume", W, True)

# write: capture / void (state machine guards double capture)
Box('Acap', Col(2), w2, BW, H, "Payment Service\nPOST /capture | /void", SVC)
Box('Acapdb', Col(3), w2, BW, H, "Payments (SQL)\nUPDATE ... WHERE\nstatus = authorized", DATA)
Box('Acappsp', Col(4), w2, BW, H, "Card processor\ncapture / void", PSP)
Arrow('Aw7', 'Ag', 'Acap', '', W, False, True)
Arrow('Aw8', 'Acap', 'Acapdb', "1 winner", W)
Arrow('Aw9', 'Acapdb', 'Acappsp', "then", W)

# write: refund (never more than was captured)
Box('Aref', Col(2), w3, BW, H, "Payment Service\nPOST /refunds\n+ Idempotency-Key", SVC)
Box('Arefdb', Col(3), w3, BW, H, "Refunds (SQL)\nkey UNIQUE; sum <=\ncaptured (row lock)", DATA)
Box('Arefpsp', Col(4), w3, BW, H, "Card processor\nrefund (async result\nvia webhook)", PSP)
Arrow('Aw10', 'Ag', 'Aref', '', W, False, True)
Arrow('Aw11', 'Aref', 'Arefdb', "insert", W)
Arrow('Aw12', 'Arefdb', 'Arefpsp', "refund", W)

# write: webhooks (async results: 3-D Secure, refunds, disputes)
Box('Awhs', Col(2), w4, BW, H, "Webhook Handler\nPOST /webhooks/psp\nverify signature", SVC)
Box('Awhd', Col(3), w4, BW, H, "Processed events (SQL)\nevent_id UNIQUE\n(dedupe)", DATA)
Box('Awhp', Col(4), w4, BW, H, "Payments / Refunds\napply status change\n+ outbox", DATA)
Arrow('Aw13', 'Ag', 'Awhs', '', W, False, True)
Arrow('Aw14', 'Awhs', 'Awhd', "dedupe", W)
Arrow('Aw15', 'Awhd', 'Awhp', "if new", W)

# write: unknown outcomes (timeouts) are QUERIED, never blindly retried
Box('Arec', Col(2), w5, BW, H, "Recovery worker\nevery 30 s: payments\nstuck 'unknown'", ASYNC)
Box('Arecq', Col(3), w5, BW, H, "Card processor\nGET by our key\n(query, not retry)", PSP)
Box('Arecdb', Col(4), w5, BW, H, "Payments (SQL)\nresolve: authorized\nor failed", DATA)
Arrow('Aw16', 'Arec', 'Arecq', "ask", W, True)
Arrow('Aw17', 'Arecq', 'Arecdb', "answer", W, True)

# write: daily reconciliation
Box('Arcj', Col(2), w6, BW, H, "Reconciliation job\ndaily 02:00", ASYNC)
Box('Arcf', Col(3), w6, BW, H, "Settlement report\n(processor) -> Blob", DATA)
Box('Arcm', Col(4), w6, BW, H, "Match by reference:\npayments + refunds\n(SQL replica)", ASYNC)
Box('Arcx', Col(5), w6, BW, H, "Mismatches (SQL)\n+ alert to ops", DATA)
Arrow('Aw18', 'Arcj', 'Arcf', "1 fetch", W, True)
Arrow('Aw19', 'Arcf', 'Arcm', "2 compare", W, True)
Arrow('Aw20', 'Arcm', 'Arcx', "3 diffs", W, True)

# read
Box('Ard', Col(2), r1, BW, H, "Payment Service\nGET /payments/{id}", SVC)
Box('Ardb', Col(3), r1, BW, H, "Payments + refunds\n(SQL primary: status\nmust be fresh)", DATA)
Box('Arr', Col(2), r2, BW, H, "Ops dashboard\nGET /reconciliation", SVC)
Box('Arrx', Col(3), r2, BW, H, "Mismatches (SQL)", DATA)
Arrow('Ar1', 'Ag', 'Ard', '', R, False, True)
Arrow('Ar2', 'Ard', 'Ardb', "", R)
Arrow('Ar3', 'Ag', 'Arr', '', R, False, True)
Arrow('Ar4', 'Arr', 'Arrx', "", R)

Note('Asum', Col(5) + 40, w3, "NEVER CHARGE TWICE, layer by layer:\n1 caller's Idempotency-Key is UNIQUE in Payments: a retry\n  gets the stored result, not a second payment\n2 the processor gets OUR paymentId as its idempotency key\n3 capture / void / refund are conditional UPDATEs on status\n4 a timeout = 'unknown': the recovery worker ASKS the\n  processor what happened instead of charging again\n5 webhooks are deduped by event_id\n\nRECONCILIATION catches whatever slipped through:\nevery day, our rows vs. the processor's settlement report.", 16, NOTE)

# =================== ROW B: FLASH SALE (same layout; numbers = order) ===================
T = 1130
Frame('fB', 0, T, 2680, 720)
Note('Bt', 20, T + 12, "B. FLASH SALE (5k payments/s for 10 min; processor allows ~2k calls/s) - numbers show the order", 24, RED)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1 = T + 610
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 575, "READ PATH", 18, R)

Box('Bos', Col(0), T + 280, BW, 80, "Order Service\n(5k checkouts/s)", EDGE)
Box('Bg', Col(1), w1, BW, r1 + H - w1, "APIM\n\nmTLS + auth,\nrate limit\nper caller", EDGE)
Arrow('Ba1', 'Bos', 'Bg', "HTTPS", None, False, True)

# write 1-4: accept fast, queue, drain at the processor's pace
Box('Bps', Col(2), w1, BW, H, "Payment Service\n1 POST /payments\n-> 202 pending", SVC)
Box('Bnx', Col(3), w1, BW, H, "Idempotency (Redis)\n2a SET key NX\n(fast dedupe)", DATA)
Box('Bdb', Col(4), w1, BW, H, "Payments (SQL, sharded\nby paymentId)\n2b insert + outbox", DATA)
Box('Bk', Col(5), w1, BW, H, "Kafka topic:\n3 payment-requests\npartition by paymentId", ASYNC)
Box('Bwk', Col(5), w2, BW, H, "Payment workers\n4 take next request", ASYNC)
Box('Brl', Col(6), w2, BW, H, "Rate limiter (Redis)\ntoken bucket: 2k\ncalls/s per account", DATA)
Box('Bcb', Col(7), w2, BW, H, "Circuit breaker\nopen -> pause, queue\nwaits (no storm)", ASYNC)
Box('Bpsp', Col(8), w2, BW, H, "Card processor\n5 authorize\nkey = paymentId", PSP)
Box('Bres', Col(7), w3, BW, H, "6 Payments: status +\noutbox -> Kafka ->\nOrder Service", DATA)
Box('Bto', Col(8), w3, BW, H, "Timeout -> 'unknown'\n-> recovery worker\nqueries (row A)", GREY)
Arrow('Bw1', 'Bg', 'Bps', '', W, False, True)
Arrow('Bw2', 'Bps', 'Bnx', "2a", W)
Arrow('Bw3', 'Bnx', 'Bdb', "2b new", W)
Arrow('Bw4', 'Bdb', 'Bk', "outbox", W, True)
Arrow('Bw5', 'Bk', 'Bwk', "consume", W, True)
Arrow('Bw6', 'Bwk', 'Brl', "token?", W)
Arrow('Bw7', 'Brl', 'Bcb', "", W)
Arrow('Bw8', 'Bcb', 'Bpsp', "call", W)
Arrow('Bw9', 'Bpsp', 'Bres', "result", W)
Arrow('Bw10', 'Bpsp', 'Bto', "timeout", RED, True)

Box('Bsame', Col(2), w3, BW, H, "Capture (in batches),\nrefunds, webhooks,\nreconciliation: as A", GREY)
Arrow('Bw11', 'Bg', 'Bsame', '', W, False, True)

# read: the shopper sees "processing" until the result event arrives
Box('Brd', Col(2), r1, BW, H, "Payment Service\nGET /payments/{id}", SVC)
Box('Brdb', Col(3), r1, BW, H, "Payments (SQL)\npending -> authorized", DATA)
Arrow('Br1', 'Bg', 'Brd', '', R, False, True)
Arrow('Br2', 'Brd', 'Brdb', "", R)

Note('Bnote', Col(4), w4 - 10, "WHY A QUEUE: the processor accepts ~2k calls/s; 5k/s sent\ndirectly = 429s -> retries -> a retry storm (and the place\ndouble charges happen). Accept fast (202), queue, and drain\nat the processor's pace; retries only on 429 / 5xx, with\nbackoff + jitter. The shopper sees 'processing' for seconds.", 16, NOTE)
