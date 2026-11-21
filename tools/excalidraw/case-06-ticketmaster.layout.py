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
Box('Ahs', Col(2), w1, BW, H, "Booking Service\nPOST /holds {seatIds}", SVC)
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

Note('Asum', Col(5) + 40, r1, "WRITE touches: hold = Booking Service -> Redis (all seats or none,\nexpire in 10 min: TEMPORARY). Checkout = Orders (pending) ->\nPayment: 1 authorize -> 2 Tickets INSERT, UNIQUE (event, seat)\n(PERMANENT, the real guard against double-selling) -> 3 capture,\nor void if a seat was taken -> outbox -> Kafka -> email.\n\nREAD touches: Search index, Event cache -> Events (SQL),\nseat map = held (Redis) + sold (SQL), orders from the replica.", 16, NOTE)

# =================== ROW B: HOT ON-SALE (only what differs) ===================
T = 1110
Frame('fB', 0, T, 2400, 690)
Note('Bt', 20, T + 12, "B. HOT ON-SALE (1M fans at 10:00:00 for 50k seats) - only what differs from row A", 24, RED)
w1, w2, w3 = T + 90, T + 210, T + 330
r1, r2 = T + 480, T + 590
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 445, "READ PATH", 18, R)

Box('Bu', Col(0), T + 300, BW, 80, "Users\n(1M at 10:00)", EDGE)
Box('Bg', Col(1), w1, BW, r2 + H - w1, "APIM (edge)\n\nbooking calls need\na valid ACCESS TOKEN;\nbot checks,\nrate limit per user", EDGE)
Arrow('Ba1', 'Bu', 'Bg', "HTTPS", None, False, True)

# write: virtual waiting room -> admit in batches -> access token
Box('Bwr', Col(2), w1, BW, H, "Waiting Room Service\nPOST /events/{id}/queue", SVC)
Box('Bq', Col(3), w1, BW, H, "Queue (Redis)\nZADD queue:{event}\nscore = arrival time", DATA)
Box('Bad', Col(4), w1, BW, H, "Admission controller\nevery 10 s admit next K\n(K = free capacity)", ASYNC)
Box('Btok', Col(5), w1, BW, H, "Access token (signed)\neventId + userId,\nvalid 15 min", EDGE)
Arrow('Bw1', 'Bg', 'Bwr', '', W, False, True)
Arrow('Bw2', 'Bwr', 'Bq', "join", W)
Arrow('Bw3', 'Bq', 'Bad', "next K", W, True)
Arrow('Bw4', 'Bad', 'Btok', "issue", W, True)

# write: holds with a per-user limit on a dedicated Redis shard
Box('Bhs', Col(2), w2, BW, H, "Booking Service\nPOST /holds (token)", SVC)
Box('Blim', Col(3), w2, BW, H, "Per-user limit (Redis)\nuser:{event}:{user}\n<= 4 seats", DATA)
Box('Bhr', Col(4), w2, BW, H, "Seat holds (Redis)\ndedicated shard for\nthis event; Lua NX", DATA)
Box('Brej', Col(5), w2, BW, H, "409: seats taken\n-> pick others", GREY)
Arrow('Bw5', 'Bg', 'Bhs', '', W, False, True)
Arrow('Bw6', 'Bhs', 'Blim', "1 limit", W)
Arrow('Bw7', 'Blim', 'Bhr', "2 hold", W)
Arrow('Bw8', 'Bhr', 'Brej', "taken", RED, True)

Box('Bsame', Col(2), w3, BW, H, "Checkout, payment,\nconfirmation:\nsame as row A", GREY)
Arrow('Bw9', 'Bg', 'Bsame', '', W, False, True)

# read: queue position; seat map from a shared snapshot
Box('Bqs', Col(2), r1, BW, H, "Waiting Room Service\nGET /queue/{token}", SVC)
Box('Bq2', Col(3), r1, BW, H, "Queue (Redis)\nZRANK -> position", DATA)
Box('Bsm', Col(2), r2, BW, H, "Event Service\nGET /events/{id}/seats", SVC)
Box('Bsnap', Col(3), r2, BW, H, "Seat map snapshot\n(CDN + Redis, 2 s)", DATA)
Box('Bhr2', Col(4), r2, BW, H, "Holds (Redis) + sold\n(SQL): rebuilt from", DATA)
Arrow('Br1', 'Bg', 'Bqs', '', R, False, True)
Arrow('Br2', 'Bqs', 'Bq2', "", R)
Arrow('Br3', 'Bg', 'Bsm', '', R, False, True)
Arrow('Br4', 'Bsm', 'Bsnap', "", R)
Arrow('Br5', 'Bsnap', 'Bhr2', "every 2 s", R, True)

Note('Bnote', Col(5) + 40, r1 - 30, "EVERYTHING ELSE IS THE SAME AS ROW A: search, event page\n(CDN-cached), checkout -> authorize -> tickets -> capture,\nconfirmation email.\n\nWHY A WAITING ROOM: 1M people hitting 'hold' at once would\nmelt the booking path and fight over the same seats. The\nqueue admits K users at a time (as many as checkout can\nserve); everyone else waits on a cheap static page.", 16, NOTE)
