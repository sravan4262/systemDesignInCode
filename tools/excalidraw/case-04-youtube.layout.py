# Case 4: YouTube. Rows: A = regular video, B = viral video. Run with excal.py.
# Write path: upload straight to Blob (SAS) -> /complete -> Kafka -> transcode orchestrator -> chunk workers.
# Read path: metadata from Video Service; the video itself streams from the CDN (HLS segments).
BW, H = 240, 70

Note('title', 0, -70, "Case 4: YouTube - user -> gateway -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Grey = same as row A", 16, NOTE)

# =================== ROW A: REGULAR VIDEO ===================
T = 0
Frame('fA', 0, T, 2560, 900)
Note('At', 20, T + 12, "A. REGULAR VIDEO (creator uploads a 10-min video; a few thousand views)", 24, W)
w1, w2, w3 = T + 90, T + 210, T + 330
R1 = T + 520
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 485, "READ PATH", 18, R)

Box('Au', Col(0), T + 380, BW, 80, "User (creator / viewer)\nweb / mobile / TV", EDGE)
Box('Araw', Col(0), T + 200, BW, H, "Blob: raw videos\n(staged blocks via SAS,\nresumable)", DATA)
Box('Acdn', Col(0), T + 580, BW, H, "CDN (Front Door)\nmanifest + segments", EDGE)
Box('Aorg', Col(0), T + 720, BW, H, "Blob: processed\n(CDN origin)", DATA)
Box('Ag', Col(1), T + 380, BW, 80, "API Gateway (APIM)\nauth, rate limit,\nroute by path", EDGE)
Arrow('Aa0', 'Au', 'Araw', "upload chunks", W)
Arrow('Aa00', 'Au', 'Acdn', "stream (ABR)", R)
Arrow('Aa01', 'Acdn', 'Aorg', "miss", R)
Arrow('Aa1', 'Au', 'Ag', "HTTPS")

# write: upload -> process
Box('Aus', Col(2), w1, BW, 100, "Upload Service\n1 POST /videos -> SAS\n2 POST /complete:\ncommit blocks", SVC)
Box('Amd', Col(3), w1, BW, H, "Video metadata (Cosmos)\nstatus: uploading ->\nprocessing + outbox", DATA)
Box('Ak', Col(4), w1, BW, H, "Kafka topic:\nvideo-uploaded", ASYNC)
Box('Aor', Col(5), w1, BW, H, "Transcode orchestrator\n(Durable Functions)\nsplit into 4-s chunks", ASYNC)
Box('Afin', Col(6), w1, BW, H, "Finalize: write manifest\n(.m3u8) + thumbnails,\nstatus = ready", ASYNC)
Box('Atw', Col(5), w2, BW, H, "Transcode workers\n(KEDA, spot VMs)\nchunk x 5 renditions", ASYNC)
Box('Apr', Col(6), w2, BW, H, "Blob: processed\nHLS segments\n240p .. 1080p", DATA)
Arrow('Aw1', 'Ag', 'Aus', "upload", W)
Arrow('Aw2', 'Aus', 'Amd', "write", W)
Arrow('Aw3', 'Amd', 'Ak', "outbox", W, True)
Arrow('Aw4', 'Ak', 'Aor', "consume", W, True)
Arrow('Aw5', 'Aor', 'Afin', "all done", W, True)
Arrow('Aw6', 'Aor', 'Atw', "fan-out jobs", W, True)
Arrow('Aw7', 'Atw', 'Apr', "write", W, True)

# write: view counts (player beacon)
Box('Avs', Col(2), w3, BW, H, "View Service\nPOST /views (beacon)", SVC)
Box('Akv', Col(3), w3, BW, H, "Kafka topic: views", ASYNC)
Box('Avc', Col(4), w3, BW, H, "Views consumer\n(consumer group)\ncount per minute", ASYNC)
Box('Avt', Col(5), w3, BW, H, "View counts (Cosmos)\nvideo + minute -> count", DATA)
Arrow('Aw8', 'Ag', 'Avs', "view", W)
Arrow('Aw9', 'Avs', 'Akv', "202", W, True)
Arrow('Aw10', 'Akv', 'Avc', "consume", W, True)
Arrow('Aw11', 'Avc', 'Avt', "1 write / min", W, True)

# read: watch page metadata (the video bytes come from the CDN)
Box('Avd', Col(2), R1, BW, 100, "Video Service\nGET /videos/{id}\n-> title + manifest URL", SVC)
Box('Ach', Col(3), R1 + 15, BW, H, "Metadata cache (Redis)\nvideo:{id}", DATA)
Box('Amd2', Col(4), R1 + 15, BW, H, "Video metadata (Cosmos)\nnearest read copy", DATA)
Arrow('Ar1', 'Ag', 'Avd', "watch page", R)
Arrow('Ar2', 'Avd', 'Ach', "1 lookup", R)
Arrow('Ar3', 'Ach', 'Amd2', "2 on miss", R)

Note('Asum', Col(5) + 40, T + 520, "WRITE touches: Upload Service, raw Blob (direct, SAS),\nVideo metadata + outbox, Kafka, orchestrator,\ntranscode workers, processed Blob, finalize step.\nViews: View Service -> Kafka -> consumer -> counts.\n\nREAD touches: Video Service, Redis, Cosmos on a miss\n(metadata only). The video bytes go CDN -> Blob origin\nand never pass through our services.", 16, NOTE)

# =================== ROW B: VIRAL VIDEO ===================
T = 980
Frame('fB', 0, T, 2560, 920)
Note('Bt', 20, T + 12, "B. VIRAL VIDEO (10M views in the first hour, ~3k views/s)", 24, RED)
w1, w2, w3 = T + 90, T + 210, T + 330
R1 = T + 520
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 485, "READ PATH", 18, R)

Box('Bu', Col(0), T + 380, BW, 80, "Users\n(3k new views/s)", EDGE)
Box('Bcdn', Col(0), T + 560, BW, H, "CDN edge\n~99% segment hits", EDGE)
Box('Bsh', Col(0), T + 680, BW, H, "Origin shield\n(1 regional cache,\ncollapses misses)", EDGE)
Box('Borg', Col(0), T + 800, BW, H, "Blob: processed\n(origin, rarely hit)", DATA)
Box('Bg', Col(1), T + 380, BW, 80, "API Gateway (APIM)\nauth, rate limit", EDGE)
Arrow('Ba00', 'Bu', 'Bcdn', "stream", R)
Arrow('Ba01', 'Bcdn', 'Bsh', "miss", R)
Arrow('Ba02', 'Bsh', 'Borg', "miss", R)
Arrow('Ba1', 'Bu', 'Bg', "HTTPS")

# write: view counts on a hot key
Box('Bvs', Col(2), w1, BW, H, "View Service\nsums per video in memory,\nsends every 5 s", SVC)
Box('Bkv', Col(3), w1, BW, H, "Kafka topic: views\npartition by videoId", ASYNC)
Box('Bvc', Col(4), w1, BW, H, "Views consumer\nsum per minute", ASYNC)
Box('Bvt', Col(5), w1, BW, H, "View counts (Cosmos)\n1 write / video / min", DATA)
Box('Bpw', Col(4), w2, BW, H, "Trending detector\npre-warm CDN POPs\n(first segments)", ASYNC)
Box('Bup', Col(2), w3, BW, H, "Upload + transcode\nsame as row A", GREY)
Arrow('Bw1', 'Bg', 'Bvs', "view", W)
Arrow('Bw2', 'Bvs', 'Bkv', "batched", W, True)
Arrow('Bw3', 'Bkv', 'Bvc', "consume", W, True)
Arrow('Bw4', 'Bvc', 'Bvt', "batch", W, True)
Arrow('Bw5', 'Bvc', 'Bpw', "views spike", W, True)
Arrow('Bw6', 'Bg', 'Bup', "upload", W)

# read: hot metadata key
Box('Bvd', Col(2), R1, BW, 100, "Video Service\n+ in-process cache 5 s\n(hot key)", SVC)
Box('Bch', Col(3), R1 + 15, BW, H, "Redis\nhot key (+ replicas)", DATA)
Box('Bmd', Col(4), R1 + 15, BW, H, "Video metadata (Cosmos)\n(almost never reached)", DATA)
Arrow('Br1', 'Bg', 'Bvd', "watch page", R)
Arrow('Br2', 'Bvd', 'Bch', "", R)
Arrow('Br3', 'Bch', 'Bmd', "on miss", R)

Note('Bsum', Col(5) + 40, T + 520, "WRITE touches: View Service (pre-aggregates, so one hot\nvideo is NOT 3k Kafka messages/s), Kafka, views consumer,\nview counts; the consumer flags trending videos and\npre-warms CDN POPs. Upload is the same as row A.\n\nREAD touches: in-process cache -> Redis -> Cosmos for\nmetadata; segments: CDN edge -> origin shield -> Blob.\nThe count shown is approximate (lags ~1 min).", 16, NOTE)
