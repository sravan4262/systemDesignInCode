# Case 5: Uber. Rows: A = normal ride, B = hot spot (stadium lets out; surge), C = ride journey. Run with excal.py.
# Requirements: fare estimate, request, nearest drivers, assignment (offer + accept), live driver location, pay at end.
# Phones -> APIM (tall box) -> WebSocket gateway (a pipe, right after APIM) for pings and pushes; REST goes APIM -> services.
BW, H = 240, 70

Note('title', 0, -70, "Case 5: Uber - driver / rider -> APIM -> WebSocket gateway / services -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Grey = same as row A", 16, NOTE)

# =================== ROW A: NORMAL RIDE ===================
T = 0
Frame('fA', 0, T, 2680, 820)
Note('At', 20, T + 12, "A. NORMAL RIDE (rider requests; ~10 available drivers within 2 km)", 24, W)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2 = T + 610, T + 720
Note('Awl', Col(3), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(3), T + 575, "READ PATH", 18, R)

Box('Ad', Col(0), w1, BW, 80, "Driver app\nlocation every 4 s", EDGE)
Box('Ar', Col(0), w4, BW, 80, "Rider app\nestimate, request,\nwatch driver", EDGE)
Box('Ag', Col(1), w1, BW, r2 + H - w1, "APIM (edge)\n\nauth (JWT),\nrate limit,\nREST -> services,\nWS -> WebSocket\ngateway", EDGE)
Box('Aws', Col(2), w1, BW, w2 + H - w1, "WebSocket gateway\n(a pipe: holds sockets,\nstores no locations)\n\npresence (Redis):\nuser -> gateway node", EDGE)
Arrow('Aa1', 'Ad', 'Ag', "WS", None, False, True)
Arrow('Aa2', 'Ar', 'Ag', "HTTPS", None, False, True)
Arrow('Aa3', 'Ag', 'Aws', "", None, False, True)

# write: ping in through the WS gateway -> geo index; if the driver has an active ride (set ONCE at accept), push straight back out
Box('Als', Col(3), w1, BW, H, "Location Service\n1 GEOADD (always)\n2 active ride? 3 push", SVC)
Box('Ageo', Col(4), w1, BW, H, "Driver geo index (Redis)\nGEOADD drivers:{city}\nstale (>30 s) swept out", DATA)
Box('Aact', Col(4), w2, BW, H, "Active rides (Redis)\nactive:{driverId} -> rider\nSET on accept, DEL on end", DATA)
Box('Apush', Col(3), w2, BW, H, "Push driverLocation\nto the rider's gateway\nnode (presence)", EDGE)
Arrow('Aw1', 'Aws', 'Als', "ping", W, False, True)
Arrow('Aw2', 'Als', 'Ageo', "1", W)
Arrow('Aw3', 'Als', 'Aact', "2 lookup", W)
Arrow('Aw4', 'Als', 'Apush', "3 if yes", R)
Arrow('Aw5', 'Apush', 'Aws', "", R, False, True)

# write: request -> match -> offer
Box('Ars', Col(3), w3, BW, H, "Ride Service\nPOST /rides -> 202", SVC)
Box('Ard', Col(4), w3, BW, H, "Rides (Azure SQL)\nstatus: matching,\nlocked price + outbox", DATA)
Box('Akr', Col(5), w3, BW, H, "Kafka topic:\nride-requested", ASYNC)
Box('Amw', Col(6), w3, BW, H, "Matching worker\nGEOSEARCH 10 nearest\nrank by ETA", ASYNC)
Box('Alk', Col(7), w3, BW, H, "Driver lock (Redis)\nSET driver:{id}\nNX EX 130", DATA)
Box('Aof', Col(8), w3, BW, H, "Offer to driver\n(via WS gateway)\n2 min, else next", EDGE)
Arrow('Aw6', 'Ag', 'Ars', '', W, False, True)
Arrow('Aw7', 'Ars', 'Ard', "insert", W)
Arrow('Aw8', 'Ard', 'Akr', "outbox", W, True)
Arrow('Aw9', 'Akr', 'Amw', "consume", W, True)
Arrow('Aw10', 'Amw', 'Alk', "lock", W)
Arrow('Aw11', 'Alk', 'Aof', "offer", W)

# write: accept / start / end -> pay at the end
Box('Ara', Col(3), w4, BW, H, "Ride Service (driver)\naccept / start / end", SVC)
Box('Ard2', Col(4), w4, BW, H, "Rides + drivers (SQL)\nUPDATE ... WHERE\nstatus = matching", DATA)
Box('Ake', Col(5), w4, BW, H, "Kafka topic:\nride-events\n(accepted, ended)", ASYNC)
Box('Apay', Col(6), w4, BW, H, "Payment Service\non ride-ended: charge\nlocked price", ASYNC)
Box('Apd', Col(7), w4, BW, H, "Payments (SQL)\nunique rideId\npending -> paid", DATA)
Box('Apsp', Col(8), w4, BW, H, "Card processor\n(Stripe / Adyen)\nidempotency key", EDGE)
Arrow('Aw12', 'Ag', 'Ara', '', W, False, True)
Arrow('Aw13', 'Ara', 'Ard2', "1 winner", W)
Arrow('Aw14', 'Ard2', 'Ake', "outbox", W, True)
Arrow('Aw15', 'Ake', 'Apay', "ended", W, True)
Arrow('Aw16', 'Apay', 'Apd', "1 pending", W)
Arrow('Aw17', 'Apd', 'Apsp', "2 charge", W)

# read
Box('Apr', Col(3), r1, BW, H, "Pricing Service\nPOST /fare-estimates", SVC)
Box('Amap', Col(4), r1, BW, H, "Maps API\nroute + ETA", DATA)
Box('Arr', Col(3), r2, BW, H, "Ride Service\nGET /rides/{id}", SVC)
Box('Ard3', Col(4), r2, BW, H, "Rides (SQL)\nread replica", DATA)
Arrow('Ar1', 'Ag', 'Apr', '', R, False, True)
Arrow('Ar2', 'Apr', 'Amap', "", R)
Arrow('Ar5', 'Ag', 'Arr', '', R, False, True)
Arrow('Ar6', 'Arr', 'Ard3', "", R)

Note('Asum', Col(5) + 40, r1 - 20, "WRITE touches: APIM -> WebSocket gateway (pipe) -> Location Service\n-> geo index (latest only); if active:{driverId} is set (Ride Service\nsets it ONCE on accept) -> push straight back out to the rider. No Kafka.\nRequest: Ride Service -> Rides (SQL) + outbox -> Kafka -> matching\nworker -> driver lock (NX) -> offer. Accept: ONE conditional UPDATE.\nEnd -> ride-ended -> Payment Service charges once.\n\nREAD touches: Pricing (Maps API), Ride Service (SQL replica).", 16, NOTE)

# =================== ROW B: HOT SPOT ===================
T = 900
Frame('fB', 0, T, 2680, 820)
Note('Bt', 20, T + 12, "B. HOT SPOT (stadium lets out: 50k ride requests in 15 min, 2k drivers nearby; surge)", 24, RED)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2 = T + 610, T + 720
Note('Bwl', Col(3), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(3), T + 575, "READ PATH", 18, R)

Box('Bd', Col(0), w1, BW, 80, "Driver apps\n(2k nearby)", EDGE)
Box('Br', Col(0), w4, BW, 80, "Rider apps\n(50k at once)", EDGE)
Box('Bg', Col(1), w1, BW, r2 + H - w1, "APIM (edge)\n\nrate limit per rider\n(no retry storms)", EDGE)
Box('Bws', Col(2), w2, BW, H, "WebSocket gateway\n(same as row A)", EDGE)
Arrow('Ba1', 'Bd', 'Bg', "WS", None, False, True)
Arrow('Ba2', 'Br', 'Bg', "HTTPS", None, False, True)
Arrow('Ba3', 'Bg', 'Bws', "", None, False, True)

# write: batch matching per zone; demand/supply drives surge
Box('Brs', Col(3), w1, BW, H, "Ride Service\nPOST /rides -> 202", SVC)
Box('Brd', Col(4), w1, BW, H, "Rides (SQL)\nstatus: matching,\nsurged price + outbox", DATA)
Box('Bk', Col(5), w1, BW, w2 + H - w1, "Kafka topics\n\nride-requested\ndriver samples\n(every 30 s, for surge)\npartition by zone", ASYNC)
Box('Bbm', Col(6), w1, BW, H, "Batch matcher (per zone)\nevery 2 s: N riders x\nM drivers, min total ETA", ASYNC)
Box('Blk', Col(7), w1, BW, H, "Driver lock (Redis)\nSET NX EX 130", DATA)
Box('Bof', Col(8), w1, BW, H, "Offers to drivers\n(the rest wait in\nqueue for next round)", EDGE)
Box('Bls', Col(3), w2, BW, H, "Location Service\n(same as A) + a sample\nper driver every 30 s", SVC)
Box('Bag', Col(6), w2, BW, H, "Demand / supply counter\nper zone, 30 s", ASYNC)
Box('Bsg', Col(7), w2, BW, H, "Surge multipliers\n(Redis) zone -> x2.1", DATA)
Box('Bgeo', Col(4), w3, BW, H, "Driver geo index (Redis)\nsharded by city", DATA)
Box('Bra', Col(3), w4, BW, H, "Accept / start / end,\nlive location, payment:\nsame as row A", GREY)
Arrow('Bw1', 'Bg', 'Brs', '', W, False, True)
Arrow('Bw2', 'Brs', 'Brd', "insert", W)
Arrow('Bw3', 'Brd', 'Bk', "outbox", W, True)
Arrow('Bw4', 'Bk', 'Bbm', "consume", W, True)
Arrow('Bw5', 'Bbm', 'Blk', "lock", W)
Arrow('Bw6', 'Blk', 'Bof', "offer", W)
Arrow('Bw7', 'Bws', 'Bls', "ping", W)
Arrow('Bw8', 'Bls', 'Bk', "sample", W, True)
Arrow('Bw9', 'Bls', 'Bgeo', "latest", W)
Arrow('Bw10', 'Bk', 'Bag', "count", W, True)
Arrow('Bw11', 'Bag', 'Bsg', "x2.1", W, True)
Arrow('Bw12', 'Bg', 'Bra', '', W, False, True)

# read: surged estimate; ride status with queue position
Box('Bpr', Col(3), r1, BW, H, "Pricing Service\nfare estimate", SVC)
Box('Bsg2', Col(4), r1, BW, H, "Surge multipliers\n(Redis) x2.1 here", DATA)
Box('Bmap', Col(5), r1, BW, H, "Maps API\n(cached per route, 1 min)", DATA)
Box('Brr', Col(3), r2, BW, H, "Ride Service\nGET /rides/{id}", SVC)
Box('Brd3', Col(4), r2, BW, H, "Rides (SQL replica)\nstatus + queue position", DATA)
Arrow('Br1', 'Bg', 'Bpr', '', R, False, True)
Arrow('Br2', 'Bpr', 'Bsg2', "1", R)
Arrow('Br3', 'Bsg2', 'Bmap', "2", R)
Arrow('Br6', 'Bg', 'Brr', '', R, False, True)
Arrow('Br7', 'Brr', 'Brd3', "", R)

Note('Bsum', Col(6) + 40, r1 - 20, "WRITE touches: same services as A, but requests in a hot zone\nare matched in BATCHES every 2 s (best overall assignment,\nnot first-come grabs); riders without a driver wait for the\nnext round. A demand / supply counter per zone sets the\nsurge multiplier; the estimate locks the surged price.\n\nREAD touches: surge (Redis) x route (Maps API, cached);\nride status + queue position from the SQL replica.", 16, NOTE)

# =================== ROW C: RIDE JOURNEY (rider and driver side by side) ===================
T = 1800
DRV = '#d0bfff'
Frame('fC', 0, T, 2680, 540)
Note('Ct', 20, T + 12, "C. RIDE JOURNEY - left to right in time: rider app (top), services (middle), driver app (bottom)", 24, W)
yR, yS, yD = T + 90, T + 250, T + 420
Note('CrL', 40, T + 58, "RIDER APP", 18, RED)
Note('CsL', 40, T + 218, "SERVICES", 18, W)


def X(i):
    return 40 + 260 * i


CW = 220
Note('CdL', X(1), T + 388, "DRIVER APP", 18, '#7048e8')
steps = [
    # (rider text, service text, driver text)
    (None, "Ride Service: available\nLocation: GEOADD", "0 Go online\npings every 4 s"),
    ("1 Enter pickup + drop\nsees $18", "Pricing Service\nMaps x rates (x surge)\nprice locked 2 min", None),
    ("2 Tap Request\n'Finding a driver...'", "Ride Service\nride = matching\n-> Kafka", None),
    (None, "Matching worker\nnearest (Redis) ->\navailable -> best ETA\n-> lock, offer", "3 Offer rings\n2 min to accept\n(decline -> next)"),
    ("4 'Maria is coming'\nCamry, 4 min", "Ride Service\naccepted + busy;\nSET active:{driver}", "4 Tap Accept"),
    ("5 Watch the car\non the map", "Location Service:\nactive ride? -> push\nto rider's gateway", "5 Drive to pickup\n(pings)"),
    ("6 'Ride started'", "Ride Service\nride = started", "6 Pick up, tap Start"),
    ("7 Watch the trip", "Same as step 5\n(live location)", "7 Drive the trip\n(pings)"),
    ("8 'You've arrived'", "Ride Service\nended, available;\nDEL active:{driver}", "8 Drop off, tap End"),
    ("9 'Paid $18'", "Payment Service\ncharge once\n(rideId unique)", "9 Sees the fare"),
]
# (rider<->service label, direction 'down' = rider->service / 'up' = service->rider), (driver<->service label, 'up' = driver->service / 'down' = service->driver)
links = [
    (None, ("POST status\n+ pings", 'up')),
    (("POST\n/fare-estimates", 'down'), None),
    (("POST /rides", 'down'), None),
    (None, ("offer (WS)", 'down')),
    (("push", 'up'), ("POST /accept", 'up')),
    (("driverLocation", 'up'), ("pings (WS)", 'up')),
    (("push", 'up'), ("POST /start", 'up')),
    (("driverLocation", 'up'), ("pings (WS)", 'up')),
    (("push", 'up'), ("POST /end", 'up')),
    (("push 'paid'", 'up'), ("push", 'down')),
]
for i, (rt, st, dt) in enumerate(steps):
    Box(f'Cs{i}', X(i), yS, CW, 90, st, SVC)
    if rt:
        Box(f'Cr{i}', X(i), yR, CW, H, rt, EDGE)
    if dt:
        Box(f'Cd{i}', X(i), yD, CW, H, dt, DRV)
    rl, dl = links[i]
    if rl:
        lab, d = rl
        if d == 'down':
            Arrow(f'Car{i}', f'Cr{i}', f'Cs{i}', lab, W)
        else:
            Arrow(f'Car{i}', f'Cs{i}', f'Cr{i}', lab, R)
    if dl:
        lab, d = dl
        if d == 'up':
            Arrow(f'Cad{i}', f'Cd{i}', f'Cs{i}', lab, W)
        else:
            Arrow(f'Cad{i}', f'Cs{i}', f'Cd{i}', lab, R)
    if i:
        Arrow(f'Cn{i}', f'Cs{i-1}', f'Cs{i}', '', NOTE, True)
