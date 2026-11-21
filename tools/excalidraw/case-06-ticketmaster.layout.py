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

# =================== ROW B: HOT ON-SALE - step by step, left to right ===================
# Three lanes: user app (top), services (middle), data (bottom). Columns = steps in time order.
T = 1110
Frame('fB', 0, T, 2420, 660)
Note('Bt', 20, T + 12, "B. HOT ON-SALE, STEP BY STEP (1M fans at 10:00:00 for 50k seats) - left to right in time", 24, RED)
yU, yS, yD = T + 90, T + 250, T + 420
Note('BuL', 40, T + 58, "USER APP", 18, RED)


def X(i):
    return 40 + 260 * i


CW = 220
Note('BsL', X(3), T + 218, "SERVICES", 18, W)
Note('BdL', X(2), T + 388, "DATA", 18, '#2b8a3e')

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
    Box(f'Bs{i}', X(i), yS, CW, 90, st, sc)
    if ut:
        Box(f'Bu{i}', X(i), yU, CW, H, ut, EDGE)
    if dt:
        Box(f'Bd{i}', X(i), yD, CW, H, dt, DATA)
        Arrow(f'Bad{i}', f'Bs{i}', f'Bd{i}', dlinks[i], W)
    if ulinks[i]:
        d, lab = ulinks[i]
        if d == 'down':
            Arrow(f'Bau{i}', f'Bu{i}', f'Bs{i}', lab, W)
        else:
            Arrow(f'Bau{i}', f'Bs{i}', f'Bu{i}', lab, R)
    if i:
        Arrow(f'Bn{i}', f'Bs{i-1}', f'Bs{i}', '', NOTE, True)

Note('Bnote', 40, T + 530, "Only admitted users get a signed ACCESS TOKEN (eventId, userId, 15 min). APIM checks it on every seat-map, hold and checkout call:\nno token / expired / wrong event -> 403, rejected at the edge. So the booking path only ever sees the K users the admission controller let in.\nSearch, the event page and confirmation work as in row A.", 16, NOTE)
