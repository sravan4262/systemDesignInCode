# Case 5: Uber. Rows: A = normal ride, B = hot spot (stadium lets out). Run with excal.py.
# Two clients (driver app, rider app) -> one tall gateway (APIM + WebSocket) -> write path (top) / read path (bottom).
BW, H = 240, 70

Note('title', 0, -70, "Case 5: Uber - driver / rider -> gateway -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Grey = same as row A", 16, NOTE)

# =================== ROW A: NORMAL RIDE ===================
T = 0
Frame('fA', 0, T, 2560, 920)
Note('At', 20, T + 12, "A. NORMAL RIDE (rider requests; ~10 available drivers within 2 km)", 24, W)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2, r3 = T + 600, T + 710, T + 820
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 565, "READ PATH", 18, R)

Box('Ad', Col(0), w1, BW, 80, "Driver app\nlocation every 4 s", EDGE)
Box('Ar', Col(0), w4, BW, 80, "Rider app\nrequest + track", EDGE)
Box('Ag', Col(1), w1, BW, r3 + H - w1, "Gateways\n\nAPIM (REST)\n+ WebSocket gateway\n(drivers online,\nriders in a ride)\n\nauth, rate limit", EDGE)
Arrow('Aa1', 'Ad', 'Ag', "WS", None, False, True)
Arrow('Aa2', 'Ar', 'Ag', "HTTPS", None, False, True)

# write: driver locations
Box('Als', Col(2), w1, BW, H, "Location Service\nWS: location ping", SVC)
Box('Ageo', Col(3), w1, BW, H, "Driver geo index (Redis)\nGEOADD drivers:{city}\nlatest only, TTL 30 s", DATA)
Box('Akl', Col(3), w2, BW, H, "Kafka topic:\ndriver-locations", ASYNC)
Box('Att', Col(4), w2, BW, H, "Trip tracker\nis this driver on a trip?", ASYNC)
Box('Apush', Col(5), w2, BW, H, "Push to rider's gateway\n(presence lookup)", EDGE)
Arrow('Aw1', 'Ag', 'Als', '', W, False, True)
Arrow('Aw2', 'Als', 'Ageo', "latest", W)
Arrow('Aw3', 'Als', 'Akl', "", W, True)
Arrow('Aw4', 'Akl', 'Att', "consume", W, True)
Arrow('Aw5', 'Att', 'Apush', "yes", W, True)

# write: request -> match -> offer
Box('Ars', Col(2), w3, BW, H, "Ride Service\nPOST /rides -> 202", SVC)
Box('Ard', Col(3), w3, BW, H, "Rides (Azure SQL)\nstatus: matching\n+ outbox", DATA)
Box('Akr', Col(4), w3, BW, H, "Kafka topic:\nride-requested", ASYNC)
Box('Amw', Col(5), w3, BW, H, "Matching worker\nGEOSEARCH 10 nearest\nrank by ETA", ASYNC)
Box('Alk', Col(6), w3, BW, H, "Driver lock (Redis)\nSET driver:{id}\nNX EX 15", DATA)
Box('Aof', Col(7), w3, BW, H, "Offer to driver (WS)\n10 s to accept,\nelse next driver", EDGE)
Arrow('Aw6', 'Ag', 'Ars', '', W, False, True)
Arrow('Aw7', 'Ars', 'Ard', "insert", W)
Arrow('Aw8', 'Ard', 'Akr', "outbox", W, True)
Arrow('Aw9', 'Akr', 'Amw', "consume", W, True)
Arrow('Aw10', 'Amw', 'Alk', "lock", W)
Arrow('Aw11', 'Alk', 'Aof', "offer", W)

# write: accept / start / end
Box('Ara', Col(2), w4, BW, H, "Ride Service\naccept / start / end", SVC)
Box('Ard2', Col(3), w4, BW, H, "Rides + drivers (SQL)\nUPDATE ... WHERE\nstatus = matching", DATA)
Box('Ake', Col(4), w4, BW, H, "Kafka topic:\nride-events", ASYNC)
Box('Acons', Col(5), w4, BW, H, "Consumers: notify rider;\non end -> Payment\n(Case 7)", ASYNC)
Arrow('Aw12', 'Ag', 'Ara', '', W, False, True)
Arrow('Aw13', 'Ara', 'Ard2', "1 winner", W)
Arrow('Aw14', 'Ard2', 'Ake', "outbox", W, True)
Arrow('Aw15', 'Ake', 'Acons', "consume", W, True)

# read
Box('Apr', Col(2), r1, BW, H, "Pricing Service\nPOST /fare-estimates", SVC)
Box('Amap', Col(3), r1, BW, H, "Maps API\nroute + ETA", DATA)
Box('Anb', Col(2), r2, BW, H, "Map Service\nGET /drivers/nearby", SVC)
Box('Ageo2', Col(3), r2, BW, H, "Driver geo index\nGEOSEARCH 2 km", DATA)
Box('Arr', Col(2), r3, BW, H, "Ride Service\nGET /rides/{id}, history", SVC)
Box('Ard3', Col(3), r3, BW, H, "Rides (SQL)\nread replica", DATA)
Arrow('Ar1', 'Ag', 'Apr', '', R, False, True)
Arrow('Ar2', 'Apr', 'Amap', "", R)
Arrow('Ar3', 'Ag', 'Anb', '', R, False, True)
Arrow('Ar4', 'Anb', 'Ageo2', "", R)
Arrow('Ar5', 'Ag', 'Arr', '', R, False, True)
Arrow('Ar6', 'Arr', 'Ard3', "", R)

Note('Asum', Col(4) + 40, r1, "WRITE touches: Location Service -> geo index (latest only)\n+ Kafka -> trip tracker -> push to the rider (only during a trip).\nRequest: Ride Service -> Rides (SQL) + outbox -> Kafka ->\nmatching worker -> driver lock (NX) -> offer. Accept: ONE\nconditional UPDATE decides the winner -> ride-events.\n\nREAD touches: Pricing (Maps API), Map Service (geo index),\nRide Service (SQL replica). Live tracking is a PUSH,\nnot a read: the rider never polls.", 16, NOTE)

# =================== ROW B: HOT SPOT ===================
T = 1000
Frame('fB', 0, T, 2560, 920)
Note('Bt', 20, T + 12, "B. HOT SPOT (stadium lets out: 50k ride requests in 15 min, 2k drivers nearby)", 24, RED)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2, r3 = T + 600, T + 710, T + 820
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 565, "READ PATH", 18, R)

Box('Bd', Col(0), w1, BW, 80, "Driver apps\n(2k nearby)", EDGE)
Box('Br', Col(0), w4, BW, 80, "Rider apps\n(50k at once)", EDGE)
Box('Bg', Col(1), w1, BW, r3 + H - w1, "Gateways\n\nAPIM + WebSocket\n\nrate limit per rider\n(no retry storms)", EDGE)
Arrow('Ba1', 'Bd', 'Bg', "WS", None, False, True)
Arrow('Ba2', 'Br', 'Bg', "HTTPS", None, False, True)

# write: requests are matched in batches per cell; demand/supply drives surge
Box('Brs', Col(2), w1, BW, H, "Ride Service\nPOST /rides -> 202", SVC)
Box('Brd', Col(3), w1, BW, H, "Rides (SQL)\nstatus: matching\n+ outbox", DATA)
Box('Bk', Col(4), w1, BW, w2 + H - w1, "Kafka topics\n\nride-requested\ndriver-locations\n\npartition by cell", ASYNC)
Box('Bbm', Col(5), w1, BW, H, "Batch matcher (per cell)\nevery 2 s: N riders x\nM drivers, min total ETA", ASYNC)
Box('Blk', Col(6), w1, BW, H, "Driver lock (Redis)\nSET NX EX 15", DATA)
Box('Bof', Col(7), w1, BW, H, "Offers to drivers\n(the rest wait in\nqueue for next round)", EDGE)
Box('Bls', Col(2), w2, BW, H, "Location Service\n(same pings as A)", SVC)
Box('Bag', Col(5), w2, BW, H, "Demand / supply counter\nper H3 cell, 30 s", ASYNC)
Box('Bsg', Col(6), w2, BW, H, "Surge multipliers\n(Redis) cell -> x2.1", DATA)
Box('Bgeo', Col(3), w3, BW, H, "Driver geo index (Redis)\nsharded by city", DATA)
Box('Bra', Col(2), w4, BW, H, "Accept / start / end\nsame as row A", GREY)
Arrow('Bw1', 'Bg', 'Brs', '', W, False, True)
Arrow('Bw2', 'Brs', 'Brd', "insert", W)
Arrow('Bw3', 'Brd', 'Bk', "outbox", W, True)
Arrow('Bw4', 'Bk', 'Bbm', "consume", W, True)
Arrow('Bw5', 'Bbm', 'Blk', "lock", W)
Arrow('Bw6', 'Blk', 'Bof', "offer", W)
Arrow('Bw7', 'Bg', 'Bls', '', W, False, True)
Arrow('Bw8', 'Bls', 'Bk', "", W, True)
Arrow('Bw9', 'Bls', 'Bgeo', "latest", W)
Arrow('Bw10', 'Bk', 'Bag', "count", W, True)
Arrow('Bw11', 'Bag', 'Bsg', "x2.1", W, True)
Arrow('Bw12', 'Bg', 'Bra', '', W, False, True)

# read: surge price, one shared nearby-cars snapshot per cell
Box('Bpr', Col(2), r1, BW, H, "Pricing Service\nfare estimate", SVC)
Box('Bsg2', Col(3), r1, BW, H, "Surge multipliers\n(Redis) x2.1 here", DATA)
Box('Bnb', Col(2), r2, BW, H, "Map Service\nnearby cars", SVC)
Box('Bsn', Col(3), r2, BW, H, "Cell snapshot cache\n(Redis, 2 s)", DATA)
Box('Bgeo2', Col(4), r2, BW, H, "Driver geo index\n(on miss)", DATA)
Box('Brr', Col(2), r3, BW, H, "Ride Service\nGET /rides/{id}", SVC)
Box('Brd3', Col(3), r3, BW, H, "Rides (SQL replica)\nstatus + queue position", DATA)
Arrow('Br1', 'Bg', 'Bpr', '', R, False, True)
Arrow('Br2', 'Bpr', 'Bsg2', "", R)
Arrow('Br3', 'Bg', 'Bnb', '', R, False, True)
Arrow('Br4', 'Bnb', 'Bsn', "", R)
Arrow('Br5', 'Bsn', 'Bgeo2', "", R)
Arrow('Br6', 'Bg', 'Brr', '', R, False, True)
Arrow('Br7', 'Brr', 'Brd3', "", R)

Note('Bsum', Col(5) + 40, r1, "WRITE touches: same services as A, but requests in a hot\ncell are matched in BATCHES every 2 s (best overall\nassignment, not first-come greedy grabs); riders who\ndon't get a driver wait for the next round. A demand /\nsupply counter per H3 cell sets the surge multiplier.\n\nREAD touches: surge from Redis; one nearby-cars snapshot\nper cell shared by everyone in the stadium (2 s);\nride status from the SQL replica.", 16, NOTE)
