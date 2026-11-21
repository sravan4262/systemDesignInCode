# Case 6: Ticketmaster. Rows: A = regular event (every path), B = hot on-sale (only what differs). Run with excal.py.
# Requirements: search events, view event (+ seat map), select seats + hold, pay, receive confirmation.
BW, H = 240, 70

Note('title', 0, -70, "Case 6: Ticketmaster - user -> APIM -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Grey = same as row A   Red = rejected / expired", 16, NOTE)

# =================== ROW A: REGULAR EVENT ===================
T = 0
Frame('fA', 0, T, 2400, 1030)
Note('At', 20, T + 12, "A. REGULAR EVENT (local concert: 2k seats, a few hundred buyers over weeks)", 24, W)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2, r3, r4 = T + 600, T + 710, T + 820, T + 930
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 565, "READ PATH", 18, R)

Box('Au', Col(0), T + 460, BW, 80, "User\nweb / mobile", EDGE)
Box('Ag', Col(1), w1, BW, r4 + H - w1, "APIM (edge)\n\nauth (JWT),\nrate limit,\nroute by path", EDGE)
Arrow('Aa1', 'Au', 'Ag', "HTTPS", None, False, True)

# write: hold seats (temporary -> Redis with TTL)
Box('Ahs', Col(2), w1, BW, H, "Booking Service\nPOST /holds {seatIds}\nDELETE /holds (cancel)", SVC)
Box('Ahr', Col(3), w1, BW, H, "Seat holds (Redis)\nLua: all seats NX\nEX 600 (10 min)", DATA)
Arrow('Aw1', 'Ag', 'Ahs', '', W, False, True)
Arrow('Aw2', 'Ahs', 'Ahr', "hold", W)

# write: checkout -> authorize -> tickets (permanent -> SQL) -> capture -> confirmation
Box('Acs', Col(2), w2, BW, H, "Booking Service\nPOST /checkout\n(hold still mine?)", SVC)
Box('Aor', Col(3), w2, BW, H, "Orders (SQL)\nstatus: pending", DATA)
Box('Apay', Col(4), w2, BW, H, "Payment Service\n1 authorize\n3 capture (or void)", SVC)
Box('Apsp', Col(5), w2, BW, H, "Card processor\n(Stripe / Adyen)\nidempotency key", EDGE)
Box('Atk', Col(4), w3, BW, H, "Tickets (SQL)\nUNIQUE (event, seat)\n+ order confirmed", DATA)
Box('Ak', Col(5), w3, BW, H, "Kafka topic:\norder-confirmed\n(via outbox)", ASYNC)
Box('Ans', Col(6), w3, BW, H, "Notification Service\n(consumer)", ASYNC)
Box('Aem', Col(7), w3, BW, H, "Email + in-app\nconfirmation", EDGE)
Arrow('Aw3', 'Ag', 'Acs', '', W, False, True)
Arrow('Aw4', 'Acs', 'Aor', "insert", W)
Arrow('Aw5', 'Aor', 'Apay', "pay", W)
Arrow('Aw6', 'Apay', 'Apsp', "1, 3", W)
Arrow('Aw7', 'Apay', 'Atk', "2 confirm", W)
Arrow('Aw8', 'Atk', 'Ak', "outbox", W, True)
Arrow('Aw9', 'Ak', 'Ans', "consume", W, True)
Arrow('Aw10', 'Ans', 'Aem', "send", W, True)
Box('Arel', Col(3), w3, BW, H, "Release hold (Redis)\nLua: DEL only if value\n= my holdId", DATA)
Box('Afail', Col(5), w1, BW, H, "Declined: KEEP hold,\nuser retries; release\non cancel or 3 fails", GREY)
Arrow('Aw13', 'Atk', 'Arel', "4 release", W)
Arrow('Aw14', 'Apay', 'Afail', "declined", RED, True)

# write: keep the search index in sync with events (events are created elsewhere)
Box('Aev', Col(3), w4, BW, H, "Events (SQL)\ncreated by organisers\n(out of scope)", DATA)
Box('Acdc', Col(4), w4, BW, H, "CDC -> search indexer", ASYNC)
Box('Asx', Col(5), w4, BW, H, "Search index\n(Azure AI Search)", DATA)
Arrow('Aw11', 'Aev', 'Acdc', "changes", W, True)
Arrow('Aw12', 'Acdc', 'Asx', "upsert", W, True)

# read
Box('Ase', Col(2), r1, BW, H, "Search Service\nGET /events?q=&city=", SVC)
Box('Asx2', Col(3), r1, BW, H, "Search index\n(Azure AI Search)", DATA)
Box('Aes', Col(2), r2, BW, H, "Event Service\nGET /events/{id}", SVC)
Box('Aec', Col(3), r2, BW, H, "Event cache (Redis)\ndetails, prices", DATA)
Box('Aev2', Col(4), r2, BW, H, "Events (SQL)\non miss", DATA)
Box('Asm', Col(2), r3, BW, H, "Event Service\nGET /events/{id}/seats", SVC)
Box('Ahr2', Col(3), r3, BW, H, "Seat holds (Redis)\nwhich seats are held", DATA)
Box('Atk2', Col(4), r3, BW, H, "Tickets (SQL)\nwhich seats are sold", DATA)
Box('Aos', Col(2), r4, BW, H, "Booking Service\nGET /orders/{id}", SVC)
Box('Aor2', Col(3), r4, BW, H, "Orders + tickets (SQL)\nread replica", DATA)
Arrow('Ar1', 'Ag', 'Ase', '', R, False, True)
Arrow('Ar2', 'Ase', 'Asx2', "query", R)
Arrow('Ar3', 'Ag', 'Aes', '', R, False, True)
Arrow('Ar4', 'Aes', 'Aec', "1", R)
Arrow('Ar5', 'Aec', 'Aev2', "2 miss", R)
Arrow('Ar6', 'Ag', 'Asm', '', R, False, True)
Arrow('Ar7', 'Asm', 'Ahr2', "held", R)
Arrow('Ar8', 'Ahr2', 'Atk2', "+ sold", R)
Arrow('Ar9', 'Ag', 'Aos', '', R, False, True)
Arrow('Ar10', 'Aos', 'Aor2', "", R)

Note('Asum', Col(5) + 40, r1, "WRITE touches: hold = Booking Service -> Redis (all seats or none,\nexpire in 10 min: TEMPORARY); cancel / TTL frees them. Checkout = Orders (pending) ->\nPayment: 1 authorize -> 2 Tickets INSERT, UNIQUE (event, seat)\n(PERMANENT, the real guard against double-selling) -> 3 capture,\nor void if a seat was taken -> 4 release the hold (DEL only if it's\nstill mine) -> outbox -> Kafka -> email. Declined card: keep the hold.\n\nREAD touches: Search index, Event cache -> Events (SQL),\nseat map = held (Redis) + sold (SQL), orders from the replica.", 16, NOTE)

# =================== ROW B: HOT ON-SALE (same layout as A; numbers = order) ===================
T = 1110
Frame('fB', 0, T, 2420, 690)
Note('Bt', 20, T + 12, "B. HOT ON-SALE (1M fans at 10:00:00 for 50k seats) - numbers show the order; the rest is as in row A", 24, RED)
w1, w2, w3 = T + 90, T + 210, T + 330
r1, r2 = T + 480, T + 590
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 445, "READ PATH", 18, R)

Box('Bwp', Col(0), w1, BW, H, "Waiting page (CDN)\nstatic; no servers hit", EDGE)
Box('Bu', Col(0), T + 300, BW, 80, "Users\n(1M at 10:00)", EDGE)
Box('Bg', Col(1), w1, BW, r2 + H - w1, "APIM (edge)\n\njoin / poll: open\n\nseat map, holds,\ncheckout: need a valid\nACCESS TOKEN\n(missing -> 403)\n\nbot checks,\nrate limit per user", EDGE)
Arrow('Ba0', 'Bu', 'Bwp', "10:00", R)
Arrow('Ba1', 'Bu', 'Bg', "HTTPS", None, False, True)

# write 1-2: join the queue, admit K every 10 s
Box('Bwr', Col(2), w1, BW, H, "Waiting Room Service\n1 POST /queue (join)\n-> queueToken", SVC)
Box('Bq', Col(3), w1, BW, H, "Queue (Redis)\nZADD queue:{event}\nscore = arrival time", DATA)
Box('Bad', Col(4), w1, BW, H, "Admission controller\n2 every 10 s: next K\n(stops when sold out)", ASYNC)
Box('Badm', Col(5), w1, BW, H, "Admitted (Redis)\nadmitted:{queueToken}\n-> signed access token", DATA)
Arrow('Bw1', 'Bg', 'Bwr', '', W, False, True)
Arrow('Bw2', 'Bwr', 'Bq', "ZADD", W)
Arrow('Bw3', 'Bq', 'Bad', "ZPOPMIN K", W, True)
Arrow('Bw4', 'Bad', 'Badm', "write", W, True)

# write 5: hold with a per-user limit on a dedicated shard
Box('Bhs', Col(2), w2, BW, H, "Booking Service\n5 POST /holds\n(token checked)", SVC)
Box('Blim', Col(3), w2, BW, H, "Per-user limit (Redis)\nuser:{event}:{user}\n<= 4 seats", DATA)
Box('Bhr', Col(4), w2, BW, H, "Seat holds (Redis)\ndedicated shard for\nthis event; Lua NX", DATA)
Box('Brej', Col(5), w2, BW, H, "409: seats taken\n-> pick others", GREY)
Arrow('Bw5', 'Bg', 'Bhs', '', W, False, True)
Arrow('Bw6', 'Bhs', 'Blim', "a limit", W)
Arrow('Bw7', 'Blim', 'Bhr', "b hold", W)
Arrow('Bw8', 'Bhr', 'Brej', "taken", RED, True)

# write 6: checkout is unchanged
Box('Bsame', Col(2), w3, BW, H, "6 Checkout, payment,\nconfirmation:\nsame as row A", GREY)
Arrow('Bw9', 'Bg', 'Bsame', '', W, False, True)

# read 3-4: poll for the token, then the seat map snapshot
Box('Bqs', Col(2), r1, BW, H, "Waiting Room Service\n3 GET /queue/{token}\npoll every 10 s", SVC)
Box('Badm2', Col(3), r1, BW, H, "Admitted (Redis)\nyes -> return token", DATA)
Box('Bq2', Col(4), r1, BW, H, "Queue (Redis)\nno -> ZRANK position", DATA)
Box('Bsm', Col(2), r2, BW, H, "Event Service\n4 GET /seats (token)", SVC)
Box('Bsnap', Col(3), r2, BW, H, "Seat map snapshot\n(CDN + Redis, 2 s)", DATA)
Box('Bhr2', Col(4), r2, BW, H, "Holds (Redis) + sold\n(SQL): rebuilt from", DATA)
Arrow('Br1', 'Bg', 'Bqs', '', R, False, True)
Arrow('Br2', 'Bqs', 'Badm2', "a", R)
Arrow('Br3', 'Badm2', 'Bq2', "b else", R)
Arrow('Br4', 'Bg', 'Bsm', '', R, False, True)
Arrow('Br5', 'Bsm', 'Bsnap', "", R)
Arrow('Br6', 'Bsnap', 'Bhr2', "every 2 s", R, True)

Note('Bnote', Col(5) + 40, r1 - 40, "ORDER: 1 join -> 2 admitted in a batch of K -> 3 poll returns\nthe access token -> 4 seat map -> 5 hold -> 6 checkout (row A).\n\nWHY A TOKEN: the queue decides who goes next; the signed token\nENFORCES it. APIM checks it on seat map, holds and checkout, so\nbots and refreshers can't skip the line: booking only ever sees\nthe K users let in. Row C shows the same flow step by step.", 16, NOTE)

# =================== ROW C: HOT ON-SALE - step by step, left to right ===================
# Three lanes: user app (top), services (middle), data (bottom). Columns = steps in time order.
T = 1880
Frame('fC', 0, T, 2420, 660)
Note('Ct', 20, T + 12, "C. HOT ON-SALE, STEP BY STEP (1M fans at 10:00:00 for 50k seats) - left to right in time", 24, RED)
yU, yS, yD = T + 90, T + 250, T + 420
Note('CuL', 40, T + 58, "USER APP", 18, RED)


def X(i):
    return 40 + 260 * i


CW = 220
Note('CsL', X(3), T + 218, "SERVICES", 18, W)
Note('CdL', X(2), T + 388, "DATA", 18, '#2b8a3e')

steps = [
    # (user text, service text, service colour, data text)
    ("1 Opens the event\npage at 10:00", "CDN\nstatic waiting page\n(no servers hit)", EDGE, None),
    ("2 Taps 'Join queue'", "APIM -> Waiting Room\nService\nPOST /queue", SVC, "Queue (Redis)\nZADD queue:{event}\nscore = arrival time"),
    ("3 Sees 'You are\n#412,000'", "Waiting Room Service\nreturns queueToken\n+ position", SVC, None),
    (None, "Admission controller\nevery 10 s: K = free\nseats + capacity", ASYNC, "Queue -> Admitted\nZPOPMIN next K ->\nadmitted:{queueToken}"),
    ("5 Polls every 10 s;\ngets an access token", "Waiting Room Service\nGET /queue/{token}\n-> signed token (JWT)", SVC, "Admitted (Redis)\nis this token in?"),
    ("6 Opens the seat map", "APIM checks token ->\nEvent Service\nGET /seats", SVC, "Seat map snapshot\n(CDN + Redis,\nrebuilt every 2 s)"),
    ("7 Picks F12 + F13:\nheld 10 min\n(409 -> pick again)", "APIM checks token ->\nBooking Service\nPOST /holds", SVC, "1 limit: <= 4 seats\n2 Lua NX hold on a\ndedicated shard"),
    ("8 Pays -> 'Confirmed'\n+ email", "Checkout, payment,\nconfirmation:\nsame as row A", GREY, "Tickets (SQL) UNIQUE\n(event, seat);\nrelease the hold"),
    ("9 Everyone still\nwaiting: 'Sold out'", "Admission controller\nfree seats = 0:\nstop admitting", ASYNC, None),
]
# arrow between user and service: ('down' = user -> service, 'up' = service -> user, label)
ulinks = [('down', "GET"), ('down', "POST"), ('up', "position"), None, ('down', "poll"),
          ('down', "GET + token"), ('down', "POST + token"), ('down', "checkout"), ('up', "push")]
dlinks = ["", "ZADD", "", "ZPOPMIN K", "lookup", "read", "hold", "INSERT", ""]

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

Note('Cnote', 40, T + 530, "Only admitted users get a signed ACCESS TOKEN (eventId, userId, 15 min). APIM checks it on every seat-map, hold and checkout call:\nno token / expired / wrong event -> 403, rejected at the edge. So the booking path only ever sees the K users the admission controller let in.\nSearch, the event page and confirmation work as in row A.", 16, NOTE)
