# Case 2: WhatsApp. Rows: A = 1:1 chat, B = group chat (256 members). Run with excal.py.
BW, H = 240, 70

Note('title', 0, -70, "Case 2: WhatsApp - user -> connection gateway -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Red = offline / skipped", 16, NOTE)

# =================== ROW A: 1:1 CHAT ===================
T = 0
Frame('fA', 0, T, 2560, 900)
Note('At', 20, T + 12, "A. 1:1 CHAT (Alice -> Bob; Bob online or offline)", 24, W)
w1, w2, w3 = T + 90, T + 200, T + 310
R1, R2 = T + 560, T + 680
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 525, "READ PATH", 18, R)

Box('Au', Col(0), T + 380, BW, 80, "User (Alice / Bob)\nmobile app", EDGE)
Box('Ab', Col(0), T + 200, BW, H, "Blob storage\n(media upload, SAS)", DATA)
Box('Acdn', Col(0), T + 560, BW, H, "CDN\n(media download)", EDGE)
Box('Ag', Col(1), T + 380, BW, 80, "Connection Gateway\nWebSocket (~200k / node)\nauth on connect", EDGE)
Arrow('Aa0', 'Au', 'Ab', "upload", W)
Arrow('Aa00', 'Au', 'Acdn', "download", R)
Arrow('Aa1', 'Au', 'Ag', "WebSocket")

# write: send a message
Box('Acs', Col(2), w1, BW, H, "Chat Service\nseq + store + ack (1 tick)", SVC)
Box('Asq', Col(3), w2, BW, H, "Seq counters (Redis)\nINCR chat:{id} = last seq", DATA)
Box('Amd', Col(3), w1, BW, H, "Messages store\npk (chat_id, month), ck seq", DATA)
Box('Ak', Col(4), w1, BW, 290, "Kafka\nmessage-sent\nreceipt-events\n(partition by chat_id)", ASYNC)
Box('Adw', Col(5), w1, BW, H, "Delivery worker\nper recipient", ASYNC)
Box('Apr', Col(6), w1, BW, H, "Presence (Redis)\nuser -> gateway node, TTL", DATA)
Box('Aon', Col(7), w1, BW, H, "ONLINE: push via\nBob's gateway", EDGE)
Box('Aoff', Col(7), w2, BW, H, "OFFLINE: Notification\nHubs (APNs / FCM)", EDGE)
Arrow('Aw1', 'Ag', 'Acs', "send", W)
Arrow('Aw2', 'Acs', 'Asq', "1 INCR", W)
Arrow('Aw3', 'Acs', 'Amd', "2 write", W)
Arrow('Aw4', 'Acs', 'Ak', "3 publish", W, True)
Arrow('Aw5', 'Ak', 'Adw', "", W, True)
Arrow('Aw6', 'Adw', 'Apr', "where is Bob?", W, True)
Arrow('Aw7', 'Adw', 'Aon', "push", W, True)
Arrow('Aw8', 'Adw', 'Aoff', "notify", RED, True)

# write: receipts (delivered / read)
Box('Ars', Col(2), w3 + 40, BW, H, "Receipt Service\ndelivered / read up to seq", SVC)
Box('Ard', Col(3), w3 + 40, BW, H, "Receipts store\n(chat, user) -> seqs", DATA)
Arrow('Aw9', 'Ag', 'Ars', "ack", W)
Arrow('Aw10', 'Ars', 'Ard', "upsert", W)
Arrow('Aw11', 'Ars', 'Ak', "event -> Alice's ticks", W, True)

# read: reconnect sync + history
Box('Ass', Col(2), R1, BW, 180, "Sync Service\non reconnect: client sends\nits last seq per chat\n+ GET history (page 50)", SVC)
Box('Auc2', Col(3), R1, BW, H, "user_chats\nmy chats (membership)", DATA)
Box('Asq2', Col(4), R1, BW, H, "Seq counters\nMGET last seq per chat", DATA)
Box('Amc', Col(3), R2, BW, H, "Recent messages cache\n(Redis, last 20 / chat)", DATA)
Box('Amd2', Col(4), R2, BW, H, "Messages store\nseq > mine (on miss)", DATA)
Arrow('Ar1', 'Ag', 'Ass', "sync", R)
Arrow('Ar2', 'Ass', 'Auc2', "1 my chats", R)
Arrow('Ar3', 'Ass', 'Asq2', "2 which changed", R)
Arrow('Ar4', 'Ass', 'Amc', "3 missed msgs", R)
Arrow('Ar5', 'Amc', 'Amd2', "miss", R)

Note('Asum', Col(5), T + 560, "WRITE touches: Gateway, Chat Service, Seq counter (INCR),\nMessages store, Kafka, Delivery worker, Presence,\nBob's gateway OR Notification Hubs. Receipts: Receipt\nService -> Receipts store -> Kafka -> Alice's gateway.\n\nREAD touches: Sync Service, user_chats, Seq counters,\nrecent cache, Messages store on a miss. Live delivery is\na PUSH (write path); reads happen on reconnect / history.", 16, NOTE)

# =================== ROW B: GROUP CHAT ===================
T = 980
Frame('fB', 0, T, 2560, 900)
Note('Bt', 20, T + 12, "B. GROUP CHAT (256 members; ~60 online)", 24, RED)
w1, w2, w3 = T + 90, T + 200, T + 310
R1, R2 = T + 560, T + 680
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 525, "READ PATH", 18, R)

Box('Bu', Col(0), T + 380, BW, 80, "Users\n(group members)", EDGE)
Box('Bg', Col(1), T + 380, BW, 80, "Connection Gateway\nWebSocket", EDGE)
Arrow('Ba1', 'Bu', 'Bg', "WebSocket")

# write: one stored copy, fan-out to 255 members
Box('Bcs', Col(2), w1, BW, H, "Chat Service\nseq + store + ack", SVC)
Box('Bsq', Col(3), w2, BW, H, "Seq counters\nINCR group:{id}", DATA)
Box('Bmd', Col(3), w1, BW, H, "Messages store\nONE copy per message", DATA)
Box('Bk', Col(4), w1, BW, 290, "Kafka\nmessage-sent\n(partition by group_id\n= order kept)", ASYNC)
Box('Bdw', Col(5), w1, BW, H, "Delivery worker\nfan-out to 255 members", ASYNC)
Box('Bgm', Col(6), w1, BW, H, "Group members cache\n(Redis set, <= 256)", DATA)
Box('Bpr', Col(6), w2, BW, H, "Presence MGET\n255 users at once", DATA)
Box('Bon', Col(7), w1, BW, H, "~60 online: ONE push\nper gateway node", EDGE)
Box('Boff', Col(7), w2, BW, H, "~195 offline:\ncollapsed notification", EDGE)
Arrow('Bw1', 'Bg', 'Bcs', "send", W)
Arrow('Bw2', 'Bcs', 'Bsq', "1 INCR", W)
Arrow('Bw3', 'Bcs', 'Bmd', "2 write", W)
Arrow('Bw4', 'Bcs', 'Bk', "3 publish", W, True)
Arrow('Bw5', 'Bk', 'Bdw', "", W, True)
Arrow('Bw6', 'Bdw', 'Bgm', "members", W, True)
Arrow('Bw7', 'Bdw', 'Bpr', "", W, True)
Arrow('Bw8', 'Bdw', 'Bon', "push", W, True)
Arrow('Bw9', 'Bdw', 'Boff', "notify", RED, True)

# write: group receipts
Box('Brs', Col(2), w3 + 40, BW, H, "Receipt Service\nread up to seq per member", SVC)
Box('Brd', Col(3), w3 + 40, BW, H, "Receipts store\nblue = min(read_seq)", DATA)
Arrow('Bw10', 'Bg', 'Brs', "ack", W)
Arrow('Bw11', 'Brs', 'Brd', "upsert", W)

# read: same sync, plus group info
Box('Bss', Col(2), R1, BW, 180, "Sync Service\nsame as 1:1\n+ group info / members\non open", SVC)
Box('Buc2', Col(3), R1, BW, H, "user_chats\nincl. group chats", DATA)
Box('Bsq2', Col(4), R1, BW, H, "Seq counters\nMGET last seq", DATA)
Box('Bmc', Col(3), R2, BW, H, "Recent messages cache\n(one per group, shared)", DATA)
Box('Bmd2', Col(4), R2, BW, H, "Messages store\n(on miss)", DATA)
Arrow('Br1', 'Bg', 'Bss', "sync", R)
Arrow('Br2', 'Bss', 'Buc2', "", R)
Arrow('Br3', 'Bss', 'Bsq2', "", R)
Arrow('Br4', 'Bss', 'Bmc', "", R)
Arrow('Br5', 'Bmc', 'Bmd2', "miss", R)

Note('Bsum', Col(5), T + 560, "WRITE touches: same as A, but the message is stored ONCE;\nthe Delivery worker reads the members set, does one\nPresence MGET for 255 users, sends one push per gateway\nnode, and one collapsed notification to offline members.\nNothing is written per member. Receipts: per member read_seq.\n\nREAD touches: Sync Service, user_chats, Seq counters,\nthe group's shared recent cache (hot), store on a miss.", 16, NOTE)
