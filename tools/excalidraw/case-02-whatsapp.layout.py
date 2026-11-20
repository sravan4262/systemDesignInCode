# Case 2: WhatsApp. Rows: A = 1:1 chat, B = group chat (256 members). Run with excal.py.
# Send flow: Chat Service stores the message (always), acks the sender, then checks presence:
# online -> push straight to the recipient's gateway; offline -> skip (the app syncs on connect).
BW, H = 240, 70

Note('title', 0, -70, "Case 2: WhatsApp - user -> connection gateway -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = best effort   Red/grey = skipped", 16, NOTE)

# =================== ROW A: 1:1 CHAT ===================
T = 0
Frame('fA', 0, T, 2560, 900)
Note('At', 20, T + 12, "A. 1:1 CHAT (Alice -> Bob; Bob online or offline)", 24, W)
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 525, "READ PATH", 18, R)

Box('Au', Col(0), T + 380, BW, 80, "User (Alice / Bob)\nmobile app", EDGE)
Box('Ab', Col(0), T + 200, BW, H, "Blob storage\n(media upload, SAS)", DATA)
Box('Acdn', Col(0), T + 560, BW, H, "CDN\n(media download)", EDGE)
Box('Ag', Col(1), T + 380, BW, 80, "Connection Gateway\nWebSocket (~200k / node)\nauth on connect", EDGE)
Arrow('Aa0', 'Au', 'Ab', "upload", W)
Arrow('Aa00', 'Au', 'Acdn', "download", R)
Arrow('Aa1', 'Au', 'Ag', "WebSocket")

# write: send a message (Chat Service does every step itself)
Box('Acs', Col(2), T + 90, BW, 250, "Chat Service\n\n1 seq = INCR\n2 store message\n3 ack Alice (1 tick)\n4 is Bob online?\n5 push or skip", SVC)
Box('Asq', Col(3), T + 90, BW, H, "Seq counters (Redis)\nINCR chat:{id}", DATA)
Box('Amd', Col(3), T + 180, BW, H, "Messages store\npk (chat_id, month), ck seq", DATA)
Box('Apr', Col(3), T + 270, BW, H, "Presence (Redis)\nuser -> gateway node, TTL", DATA)
Box('Aon', Col(4), T + 220, BW, H, "ONLINE: push via\nBob's gateway", EDGE)
Box('Aoff', Col(4), T + 310, BW, H, "OFFLINE: skip\n(stays stored; syncs\nwhen Bob connects)", GREY)
Arrow('Aw1', 'Ag', 'Acs', "send", W)
Arrow('Aw2', 'Acs', 'Asq', "1 INCR", W)
Arrow('Aw3', 'Acs', 'Amd', "2 store", W)
Arrow('Aw4', 'Acs', 'Apr', "4 lookup", W)
Arrow('Aw5', 'Apr', 'Aon', "found", W, True)
Arrow('Aw6', 'Apr', 'Aoff', "not found", RED, True)

# write: receipts (delivered / read)
Box('Ars', Col(2), T + 395, BW, H, "Receipt Service\ndelivered / read up to seq", SVC)
Box('Ard', Col(3), T + 395, BW, H, "Receipts store\n(chat, user) -> seqs", DATA)
Box('Arn', Col(4), T + 395, BW, H, "Push receipt to Alice\n(if online, else sync)", EDGE)
Arrow('Aw9', 'Ag', 'Ars', "ack", W)
Arrow('Aw10', 'Ars', 'Ard', "upsert", W)
Arrow('Aw11', 'Ard', 'Arn', "then", W, True)

# read: connect first, then sync; history
Box('Ass', Col(2), T + 560, BW, 180, "Sync Service\non connect: client sends\nits last seq per chat\n+ GET history (page 50)", SVC)
Box('Auc2', Col(3), T + 560, BW, H, "user_chats\nmy chats (membership)", DATA)
Box('Asq2', Col(4), T + 560, BW, H, "Seq counters\nMGET last seq per chat", DATA)
Box('Amc', Col(3), T + 680, BW, H, "Recent messages cache\n(Redis, last 20 / chat)", DATA)
Box('Amd2', Col(4), T + 680, BW, H, "Messages store\nseq > mine (on miss)", DATA)
Arrow('Ar1', 'Ag', 'Ass', "sync", R)
Arrow('Ar2', 'Ass', 'Auc2', "1 my chats", R)
Arrow('Ar3', 'Auc2', 'Asq2', "2 which changed", R)
Arrow('Ar4', 'Ass', 'Amc', "3 missed msgs", R)
Arrow('Ar5', 'Amc', 'Amd2', "miss", R)

Note('Asum', Col(5) + 40, T + 120, "WRITE touches: Gateway -> Chat Service -> Seq counter,\nMessages store (ALWAYS stored before the 1-tick ack),\nPresence -> Bob's gateway if online, otherwise skip.\nReceipts: Receipt Service -> Receipts store -> push to\nAlice if online.\n\nREAD touches: Sync Service, user_chats, Seq counters,\nrecent cache, Messages store on a miss.\nThe app opens the WebSocket FIRST, then syncs, and drops\nduplicates by (chatId, seq).", 16, NOTE)

# =================== ROW B: GROUP CHAT ===================
T = 980
Frame('fB', 0, T, 2560, 900)
Note('Bt', 20, T + 12, "B. GROUP CHAT (256 members; ~60 online)", 24, RED)
Note('Bwl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(2), T + 525, "READ PATH", 18, R)

Box('Bu', Col(0), T + 380, BW, 80, "Users\n(group members)", EDGE)
Box('Bg', Col(1), T + 380, BW, 80, "Connection Gateway\nWebSocket", EDGE)
Arrow('Ba1', 'Bu', 'Bg', "WebSocket")

# write: one stored copy, Chat Service fans out to online members
Box('Bcs', Col(2), T + 90, BW, 250, "Chat Service\n\n1 seq = INCR\n2 store ONCE\n3 ack sender\n4 who is online?\n5 push to online only", SVC)
Box('Bsq', Col(3), T + 90, BW, H, "Seq counters\nINCR group:{id}", DATA)
Box('Bmd', Col(3), T + 180, BW, H, "Messages store\nONE copy per message", DATA)
Box('Bgm', Col(3), T + 270, BW, H, "Group members cache\n(Redis set, <= 256)", DATA)
Box('Bpr', Col(4), T + 270, BW, H, "Presence MGET\n255 users at once", DATA)
Box('Bon', Col(5), T + 220, BW, H, "~60 online: ONE push\nper gateway node", EDGE)
Box('Boff', Col(5), T + 310, BW, H, "~195 offline: skip\n(sync on connect)", GREY)
Arrow('Bw1', 'Bg', 'Bcs', "send", W)
Arrow('Bw2', 'Bcs', 'Bsq', "1 INCR", W)
Arrow('Bw3', 'Bcs', 'Bmd', "2 store", W)
Arrow('Bw4', 'Bcs', 'Bgm', "4 members", W)
Arrow('Bw5', 'Bgm', 'Bpr', "MGET 255", W)
Arrow('Bw6', 'Bpr', 'Bon', "found", W, True)
Arrow('Bw7', 'Bpr', 'Boff', "not found", RED, True)

# write: group receipts
Box('Brs', Col(2), T + 395, BW, H, "Receipt Service\nread up to seq per member", SVC)
Box('Brd', Col(3), T + 395, BW, H, "Receipts store\nblue = min(read_seq)", DATA)
Box('Brn', Col(4), T + 395, BW, H, "Push ticks to sender\n(if online, else sync)", EDGE)
Arrow('Bw10', 'Bg', 'Brs', "ack", W)
Arrow('Bw11', 'Brs', 'Brd', "upsert", W)
Arrow('Bw12', 'Brd', 'Brn', "then", W, True)

# read: same sync, plus group info
Box('Bss', Col(2), T + 560, BW, 180, "Sync Service\nsame as 1:1\n+ group info / members\non open", SVC)
Box('Buc2', Col(3), T + 560, BW, H, "user_chats\nincl. group chats", DATA)
Box('Bsq2', Col(4), T + 560, BW, H, "Seq counters\nMGET last seq", DATA)
Box('Bmc', Col(3), T + 680, BW, H, "Recent messages cache\n(one per group, shared)", DATA)
Box('Bmd2', Col(4), T + 680, BW, H, "Messages store\n(on miss)", DATA)
Arrow('Br1', 'Bg', 'Bss', "sync", R)
Arrow('Br2', 'Bss', 'Buc2', "", R)
Arrow('Br3', 'Buc2', 'Bsq2', "", R)
Arrow('Br4', 'Bss', 'Bmc', "", R)
Arrow('Br5', 'Bmc', 'Bmd2', "miss", R)

Note('Bsum', Col(6) + 40, T + 120, "WRITE touches: same as A, but the message is stored ONCE;\nthe Chat Service reads the members set, does one\nPresence MGET for 255 users and sends one push per\ngateway node. Offline members are skipped and sync later.\nReceipts: per member read_seq; blue = everyone read.\n\nREAD touches: Sync Service, user_chats, Seq counters,\nthe group's shared recent cache (hot), store on a miss.", 16, NOTE)
