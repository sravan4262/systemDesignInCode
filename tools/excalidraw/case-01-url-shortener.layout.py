# Case 1: URL shortener. Rows: A = regular link, B = viral link. Run with excal.py.
BW, H = 240, 70

Note('title', 0, -70, "Case 1: URL shortener - user -> edge -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Red = never cached / skipped", 16, NOTE)

# =================== ROW A: REGULAR LINK ===================
T = 0
Frame('fA', 0, T, 2560, 820)
Note('At', 20, T + 12, "A. REGULAR LINK (created by Alice, clicked a few times a day worldwide)", 24, W)
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 415, "READ PATH", 18, R)

Box('Au', Col(0), T + 320, BW, 80, "User\ncreator / clicker", EDGE)
Box('Afd', Col(1), T + 320, BW, 80, "Front Door (edge)\nTLS, WAF, route by path,\nshort-TTL 302 cache", EDGE)
Arrow('Aa1', 'Au', 'Afd', "HTTPS")

# write: create a link
Box('Aapim', Col(2), T + 90, BW, H, "APIM\nPOST /urls\nauth, rate limit", EDGE)
Box('Acs', Col(3), T + 90, BW, H, "Create Service\nrandom 7-char base62\nretry on 409 (max 5)", SVC)
Box('Adb', Col(4), T + 90, BW, H, "Cosmos DB: links\nWRITE region (East US)\ninsert id = code", DATA)
Note('Awhy', Col(4), T + 172, "Why Cosmos: fast key lookups +\nbuilt-in multi-region read copies", 15, NOTE)
Note('Arepl', Col(5), T + 2, "async replication (~1 s)", 18, W)
Box('Aeu', Col(5), T + 32, BW, 56, "Read copy: West Europe", DATA)
Box('Ain', Col(5), T + 102, BW, 56, "Read copy: Central India", DATA)
Box('Ajp', Col(5), T + 172, BW, 56, "Read copy: Japan East", DATA)
Arrow('Aw1', 'Afd', 'Aapim', "create", W)
Arrow('Aw2', 'Aapim', 'Acs', "", W)
Arrow('Aw3', 'Acs', 'Adb', "insert\n(409 = taken)", W)
Arrow('Aw4a', 'Adb', 'Aeu', "", W, True)
Arrow('Aw4b', 'Adb', 'Ain', "", W, True)
Arrow('Aw4c', 'Adb', 'Ajp', "", W, True)

# write: click events (sent by the redirect service)
Box('Aeh', Col(3), T + 290, BW, H, "Kafka topic: clicks\n(Azure Event Hubs)", ASYNC)
Box('Aag', Col(4), T + 290, BW, H, "Clicks consumer\n(consumer group)\ncount per code per minute", ASYNC)
Box('Ast', Col(5), T + 290, BW, H, "Click counts table\n(Cosmos) code + minute\n-> count", DATA)
Arrow('Aw6', 'Aeh', 'Aag', "consume", W, True)
Arrow('Aw7', 'Aag', 'Ast', "1 write / min", W, True)

# read: redirect (chained so the arrows don't overlap) + stats
Box('Ars', Col(2), T + 450, BW, 100, "Redirect Service\nGET /{code} -> 302\n404 / 410 (expired)", SVC)
Box('Ach', Col(3), T + 465, BW, H, "Redis (per region)\ncode -> longUrl\ncache hits only", DATA)
Box('Adr', Col(4), T + 465, BW, H, "Cosmos nearest\nread copy", DATA)
Box('Adw', Col(5), T + 465, BW, H, "Cosmos write region\n(brand-new code only)", DATA)
Box('Ass', Col(2), T + 650, BW, H, "Stats Service\nGET /urls/{code}/stats", SVC)
Box('Ast2', Col(3), T + 650, BW, H, "Click counts table\n(owner reads)", DATA)
Arrow('Ar1', 'Afd', 'Ars', "redirect", R)
Arrow('Ar2', 'Ars', 'Ach', "1 lookup", R)
Arrow('Ar3', 'Ach', 'Adr', "2 on miss", R)
Arrow('Ar4', 'Adr', 'Adw', "3 not found", R, True)
Arrow('Aw5', 'Ars', 'Aeh', "click event\n(buffered)", W, True)
Box('Aapim2', Col(1), T + 650, BW, H, "APIM\nGET /urls/{code}/stats\nauth (owner only)", EDGE)
Arrow('Ar5', 'Afd', 'Aapim2', "stats", R)
Arrow('Ar5b', 'Aapim2', 'Ass', "", R)
Arrow('Ar6', 'Ass', 'Ast2', "read", R)

Note('Asum', Col(6) + 40, T + 450, "WRITE touches: APIM, Create Service, Cosmos write region\n(async copy to 3 read regions); clicks: Redirect Service\n-> Kafka topic -> clicks consumer -> click counts table.\n\nREAD touches: Front Door, Redirect Service, Redis,\nnearest Cosmos read copy (miss), write region only\nwhen a brand-new code is not copied yet.\nNever cache a 404.", 16, NOTE)

# =================== ROW B: VIRAL LINK ===================
T = 900
Frame('fB', 0, T, 2560, 860)
Note('Bt', 20, T + 12, "B. VIRAL LINK (one code, 50k clicks/s after a celebrity posts it)", 24, RED)
w1, w2 = T + 90, T + 200
R1, R2 = T + 470, T + 600
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2) + 130, T + 440, "READ PATH", 18, R)

Box('Bu', Col(0), T + 340, BW, 80, "Users\n(50k clicks/s)", EDGE)
Box('Bfd', Col(1), T + 340, BW, 80, "Front Door\ncaches the 302,\nTTL 60 s (<= expiry)", EDGE)
Arrow('Ba1', 'Bu', 'Bfd', "HTTPS")

# write: clicks counted from edge logs (cached clicks never reach origin)
Box('Blog', Col(2), w1, BW, H, "Front Door access logs\nevery click, incl. cached", ASYNC)
Box('Beh', Col(3), w1, BW, H, "Kafka topic: clicks\npartition by code", ASYNC)
Box('Bag', Col(4), w1, BW, H, "Clicks consumer\npre-aggregates in memory", ASYNC)
Box('Bst', Col(5), w1, BW, H, "Click counts table\n1 write / code / min", DATA)
Arrow('Bw1', 'Bfd', 'Blog', "log", W, True)
Arrow('Bw2', 'Blog', 'Beh', "", W, True)
Arrow('Bw3', 'Beh', 'Bag', "consume", W, True)
Arrow('Bw4', 'Bag', 'Bst', "batch", W, True)

# write: delete / abuse takedown
Box('Bapim', Col(2), w2 + 40, BW, H, "APIM\nDELETE /urls/{code}\n(owner / abuse)", EDGE)
Box('Bdel', Col(3), w2 + 40, BW, H, "Create Service\nsoft delete", SVC)
Box('Bdb', Col(4), w2 + 40, BW, H, "Cosmos links\nmark deleted -> 410", DATA)
Box('Bpur', Col(5), w2 + 40, BW, H, "Front Door purge\n/{code} + Redis DEL", EDGE)
Arrow('Bw5', 'Bfd', 'Bapim', "delete", W)
Arrow('Bw6', 'Bapim', 'Bdel', "", W)
Arrow('Bw7', 'Bdel', 'Bdb', "", W)
Arrow('Bw8', 'Bdb', 'Bpur', "then purge", W)

# read: the edge absorbs almost everything
Box('Bhit', Col(2), R1, BW, H, "Edge cache HIT\n~99% of clicks end here", EDGE)
Box('Brs', Col(2), R2, BW, 100, "Redirect Service\n+ in-process cache 5 s\n(hot key)", SVC)
Box('Bch', Col(3), R2 + 15, BW, H, "Redis\nhot key (+ replicas)", DATA)
Box('Bdb2', Col(4), R2 + 15, BW, H, "Cosmos\n(almost never reached)", DATA)
Arrow('Br1', 'Bfd', 'Bhit', "cached", R)
Arrow('Br2', 'Bfd', 'Brs', "miss / TTL expired", R)
Arrow('Br3', 'Brs', 'Bch', "", R)
Arrow('Br4', 'Bch', 'Bdb2', "on miss", R)

Note('Bsum', Col(6) + 40, T + 470, "WRITE touches: Front Door logs -> Kafka topic -> clicks\nconsumer -> click counts table; delete = APIM -> Create\nService -> Cosmos, then Front Door purge + Redis DEL.\n\nREAD touches: Front Door cache (most clicks), then\nRedirect Service in-process cache -> Redis -> Cosmos.\nThe origin sees ~1 request per edge POP per minute.", 16, NOTE)
