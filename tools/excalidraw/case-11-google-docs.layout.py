# Case 11: Google Docs. Rows: A = edit lock (one editor), B = real-time co-editing (every path), C = hot document (only what differs).
# Requirements: create / open, edit (lock or co-edit), presence + cursors, version history, sharing. Run with excal.py.
BW, H = 240, 70

Note('title', 0, -70, "Case 11: Google Docs - user -> APIM (REST) / WebSocket gateway (live) -> WRITE path vs. READ path", 28)
Note('legend', 0, -25, "Blue = write path   Green = read path   Dashed = async   Grey = same as an earlier row   Red = rejected", 16, NOTE)

# =================== ROW A: EDIT LOCK (one editor at a time) ===================
T = 0
Frame('fA', 0, T, 2400, 920)
Note('At', 20, T + 12, "A. EDIT LOCK: click 'Edit' -> only you can edit; everyone else is read-only", 24, W)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2, r3 = T + 600, T + 710, T + 820
Note('Awl', Col(2), T + 55, "WRITE PATH", 18, W)
Note('Arl', Col(2), T + 565, "READ PATH", 18, R)

Box('Au', Col(0), T + 420, BW, 80, "Users\n(1 editor, others\nviewing)", EDGE)
Box('Ag', Col(1), w1, BW, r3 + H - w1, "APIM\n\nauth (JWT),\nrate limit", EDGE)
Arrow('Aa1', 'Au', 'Ag', "HTTPS", None, False, True)

# write: take the lock (lease + fencing token)
Box('Alk', Col(2), w1, BW, H, "Document Service\nPOST /docs/{id}/lock", SVC)
Box('Alr', Col(3), w1, BW, H, "Locks (Redis)\nSET lock:{doc} user NX\nEX 120 + fencing token", DATA)
Box('Alt', Col(4), w1, BW, H, "423 Locked:\n'Priya is editing'\n-> read-only", GREY)
Arrow('Aw1', 'Ag', 'Alk', '', W, False, True)
Arrow('Aw2', 'Alk', 'Alr', "lock", W)
Arrow('Aw3', 'Alr', 'Alt', "taken", RED, True)

# write: keep it alive / give it back
Box('Arn', Col(2), w2, BW, H, "Document Service\nrenew every 30 s,\nDELETE on close", SVC)
Box('Arr', Col(3), w2, BW, H, "Locks (Redis)\nonly if still mine\n(compare-and-delete)", DATA)
Arrow('Aw4', 'Ag', 'Arn', '', W, False, True)
Arrow('Aw5', 'Arn', 'Arr', "renew / release", W)

# write: save (content to Blob, pointer to SQL with a version check)
Box('Asv', Col(2), w3, BW, H, "Document Service\nPUT /content\n(lockToken, version)", SVC)
Box('Abl', Col(3), w3, BW, H, "Content (Blob)\n1 new object v43", DATA)
Box('Adb', Col(4), w3, BW, H, "Docs (SQL)\n2 UPDATE WHERE v = 42\nAND token = current", DATA)
Box('Avr', Col(5), w3, BW, H, "Versions (SQL)\nv43 -> blob, by Priya", DATA)
Arrow('Aw6', 'Ag', 'Asv', '', W, False, True)
Arrow('Aw7', 'Asv', 'Abl', "1", W)
Arrow('Aw8', 'Abl', 'Adb', "2", W)
Arrow('Aw9', 'Adb', 'Avr', "3", W)

# write: sharing
Box('Ash', Col(2), w4, BW, H, "Document Service\nPUT /permissions", SVC)
Box('Aacl', Col(3), w4, BW, H, "ACL (SQL)\n(doc, user) -> role", DATA)
Arrow('Aw10', 'Ag', 'Ash', '', W, False, True)
Arrow('Aw11', 'Ash', 'Aacl', "write", W)

# read
Box('Aop', Col(2), r1, BW, H, "Document Service\nGET /docs/{id}", SVC)
Box('Adb2', Col(3), r1, BW, H, "Docs + ACL (SQL)\nallowed? latest version", DATA)
Box('Abl2', Col(4), r1, BW, H, "Content (Blob)\nlatest version", DATA)
Box('Alq', Col(2), r2, BW, H, "Document Service\nGET /docs/{id}/lock", SVC)
Box('Alq2', Col(3), r2, BW, H, "Locks (Redis)\nwho is editing?", DATA)
Box('Avq', Col(2), r3, BW, H, "Document Service\nGET /versions", SVC)
Box('Avq2', Col(3), r3, BW, H, "Versions (SQL)", DATA)
Box('Avq3', Col(4), r3, BW, H, "Content (Blob)\nthat version", DATA)
Arrow('Ar1', 'Ag', 'Aop', '', R, False, True)
Arrow('Ar2', 'Aop', 'Adb2', "1", R)
Arrow('Ar3', 'Adb2', 'Abl2', "2", R)
Arrow('Ar4', 'Ag', 'Alq', '', R, False, True)
Arrow('Ar5', 'Alq', 'Alq2', "", R)
Arrow('Ar6', 'Ag', 'Avq', '', R, False, True)
Arrow('Ar7', 'Avq', 'Avq2', "", R)
Arrow('Ar8', 'Avq2', 'Avq3', "", R)

Note('Asum', Col(5) + 40, r1, "LOCK = a LEASE: 2 min, renewed every 30 s; a closed laptop\nfrees it by itself. The FENCING TOKEN (a number that grows\nwith every new lock) is checked on save, so an editor whose\nlock expired can't overwrite the new editor's work.\n\nSAVE: write the new content to Blob, then move the pointer\nin SQL only if the version is still 42 (optimistic check).\nNo merging ever: one writer at a time.", 16, NOTE)

# =================== ROW B: REAL-TIME CO-EDITING ===================
T = 1000
Frame('fB', 0, T, 2400, 810)
Note('Bt', 20, T + 12, "B. REAL-TIME CO-EDITING (2-5 people typing in the same doc at once; no locks)", 24, W)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1, r2 = T + 600, T + 710
Note('Bwl', Col(3), T + 55, "WRITE PATH", 18, W)
Note('Brl', Col(3), T + 565, "READ PATH", 18, R)

Box('Bu', Col(0), T + 380, BW, 80, "Editors\n(2-5 typing)", EDGE)
Box('Bg', Col(1), w1, BW, r2 + H - w1, "APIM\n\nREST -> services\nWS -> WebSocket\ngateway", EDGE)
Box('Bws', Col(2), w1, BW, w3 + H - w1, "WebSocket gateway\n(a pipe)\n\nsends every socket\nof a doc to that\ndoc's OWNER node", EDGE)
Arrow('Ba1', 'Bu', 'Bg', "HTTPS + WS", None, False, True)
Arrow('Ba2', 'Bg', 'Bws', "", None, False, True)

# write: one owner node per doc orders every edit
Box('Bcol', Col(3), w1, BW, H, "Collab Service (owner)\n1 transform (OT)\n2 rev = n + 1", SVC)
Box('Bop', Col(4), w1, BW, H, "Op log (Cosmos)\n3 append (doc, rev)\nclientOpId unique", DATA)
Box('Bsn', Col(5), w1, BW, H, "Snapshotter\nevery 500 ops / 1 min", ASYNC)
Box('Bbl', Col(6), w1, BW, H, "Snapshots (Blob)\ndoc state at rev n", DATA)
Box('Bbc', Col(3), w2, BW, H, "4 ack + broadcast:\n1 message per gateway\nnode -> its sockets", EDGE)
Box('Bpr', Col(4), w2, BW, H, "Connections (Redis)\nconns:{doc}: user ->\ngateway node, cursor", DATA)
Box('Brt', Col(3), w3, BW, H, "Doc router (Redis)\ndoc -> owner node;\njoin registers conn", DATA)
Arrow('Bw1', 'Bws', 'Bcol', "op", W, False, True)
Arrow('Bw2', 'Bcol', 'Bop', "3", W)
Arrow('Bw3', 'Bop', 'Bsn', "", W, True)
Arrow('Bw4', 'Bsn', 'Bbl', "write", W, True)
Arrow('Bw5', 'Bcol', 'Bbc', "4", W)
Arrow('Bw6', 'Bbc', 'Bws', "push", R, True, True)
Arrow('Bw7', 'Bcol', 'Bpr', "where to send", W)
Arrow('Bw7b', 'Brt', 'Bpr', "register", W)
Arrow('Bw8', 'Bws', 'Brt', "which node?", W, False, True)

Box('Bds', Col(3), w4, BW, H, "Document Service\ncreate, share, restore", SVC)
Box('Bdb', Col(4), w4, BW, H, "Docs, ACL, versions\n(SQL)", DATA)
Arrow('Bw9', 'Bg', 'Bds', '', W, False, True)
Arrow('Bw10', 'Bds', 'Bdb', "write", W)

# read: open = latest snapshot + the ops after it
Box('Bop2', Col(3), r1, BW, H, "Document Service\nGET /docs/{id} (open)", SVC)
Box('Bdb2', Col(4), r1, BW, H, "Docs + ACL (SQL)\nallowed? snapshot rev", DATA)
Box('Bbl2', Col(5), r1, BW, H, "Snapshot (Blob)\nrev 9,500", DATA)
Box('Bot', Col(6), r1, BW, H, "Op log tail\nrevs 9,501 .. now", DATA)
Box('Bvq', Col(3), r2, BW, H, "Document Service\nGET /versions", SVC)
Box('Bvq2', Col(4), r2, BW, H, "Versions (SQL)\n-> snapshot pointers", DATA)
Arrow('Br1', 'Bg', 'Bop2', '', R, False, True)
Arrow('Br2', 'Bop2', 'Bdb2', "1", R)
Arrow('Br3', 'Bdb2', 'Bbl2', "2", R)
Arrow('Br4', 'Bbl2', 'Bot', "3 + ops", R)
Arrow('Br5', 'Bg', 'Bvq', '', R, False, True)
Arrow('Br6', 'Bvq', 'Bvq2', "", R)

Note('Bsum', Col(5) + 40, w2 + 10, "EVERY EDIT: client sends { baseRev, op } -> the doc's owner node\n1 transforms it against ops the client hasn't seen (OT)\n2 gives it the next rev, 3 appends it to the op log,\n4 acks the sender and broadcasts: conns:{doc} says which\ngateway node each editor is on -> 1 message per node.\nOne node orders a doc's edits, so everyone converges.\n\nOPEN: latest snapshot + the ops after it. Owner node dies ->\nits lease expires, another node rebuilds from snapshot + log.", 16, NOTE)

# =================== ROW C: HOT DOCUMENT (only what differs; numbers = order) ===================
T = 1890
Frame('fC', 0, T, 2400, 810)
Note('Ct', 20, T + 12, "C. HOT DOCUMENT (1,000 people open it, ~50 editing) - only what differs from row B; numbers = order", 24, RED)
w1, w2, w3, w4 = T + 90, T + 210, T + 330, T + 450
r1 = T + 600
Note('Cwl', Col(3), T + 55, "WRITE PATH", 18, W)
Note('Crl', Col(3), T + 565, "READ PATH", 18, R)

Box('Cu', Col(0), T + 380, BW, 80, "1,000 users\n(~50 editing)", EDGE)
Box('Cg', Col(1), w1, BW, r1 + H - w1, "APIM\n\nREST -> services\nWS -> WebSocket\ngateway", EDGE)
Box('Cws', Col(2), w2, BW, w3 + H - w2, "WebSocket gateway\nnodes (many)", EDGE)
Arrow('Ca1', 'Cu', 'Cg', "HTTPS + WS", None, False, True)
Arrow('Ca2', 'Cg', 'Cws', "", None, False, True)

Box('Cjn', Col(3), w1, BW, H, "1 Document Service\njoin: editor if <= 100,\nelse viewer", SVC)
Box('Ced', Col(4), w1, BW, H, "Editors (Redis)\nINCR editors:{doc}", DATA)
Arrow('Cw1', 'Cg', 'Cjn', '', W, False, True)
Arrow('Cw2', 'Cjn', 'Ced', "count", W)

Box('Ccol', Col(3), w2, BW, H, "2 Collab Service (owner)\nbatch ops every 50 ms", SVC)
Box('Cop', Col(4), w2, BW, H, "Op log (Cosmos)\n1 write per batch\n(group commit)", DATA)
Box('Cpb', Col(3), w3, BW, H, "3 publish each batch\nonce", SVC)
Box('Cps', Col(4), w3, BW, H, "Pub/sub channel doc:{id}\n(Azure Web PubSub /\nRedis pub/sub)", ASYNC)
Box('Cfn', Col(5), w3, BW, H, "4 gateway nodes with\nviewers subscribe;\npush (throttled 500 ms)", EDGE)
Arrow('Cw3', 'Cws', 'Ccol', "ops", W, False, True)
Arrow('Cw4', 'Ccol', 'Cop', "batch", W)
Arrow('Cw5', 'Ccol', 'Cpb', "", W)
Arrow('Cw6', 'Cpb', 'Cps', "", W)
Arrow('Cw7', 'Cps', 'Cfn', "fan out", R, True)

Box('Csame', Col(3), w4, BW, H, "Sharing, versions,\nsnapshots: as row B", GREY)
Arrow('Cw8', 'Cg', 'Csame', '', W, False, True)

Box('Clt', Col(3), r1, BW, H, "5 Late joiner\nopens the doc", SVC)
Box('Csn', Col(4), r1, BW, H, "Snapshot (Blob + CDN)\nevery 30 s for hot docs", DATA)
Box('Ctl', Col(5), r1, BW, H, "Op log tail\n(short)", DATA)
Arrow('Cr1', 'Cg', 'Clt', '', R, False, True)
Arrow('Cr2', 'Clt', 'Csn', "1", R)
Arrow('Cr3', 'Csn', 'Ctl', "2", R)

Note('Csum', Col(5) + 40, w1, "WHAT CHANGES: 1 cap live editors at 100 (the rest view only),\n2 the owner batches ops every 50 ms and writes the op log\nonce per batch, 3 publishes each batch ONCE to a pub/sub\nchannel, 4 every gateway node fans it out to its own viewers,\nthrottled; viewers see no live cursors. 5 late joiners load a\nCDN-cached snapshot taken every 30 s, plus a short tail.", 16, NOTE)
