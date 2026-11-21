# M25 practical build: AI chat over company documents (RAG) for logged-in employees.
# Rows: A = normal day (ingestion = write path, asking = read path), B = all-hands spike (only what differs; numbers = order).
BW, H = 240, 70
AI = '#d0bfff'

Note('title', 0, -70, "M25: AI chat over company documents - employee -> APIM -> INGEST documents vs. ASK a question", 28)
Note('legend', 0, -25, "Blue = write path (ingestion)   Green = read path (asking)   Dashed = async   Purple = AI model call   Grey = same as row A", 16, NOTE)

# =================== ROW A: NORMAL DAY ===================
T = 0
Frame('fA', 0, T, 2400, 880)
Note('At', 20, T + 12, "A. NORMAL DAY (20k employees, ~100k questions/day; documents change a few hundred times a day)", 24, W)
w1, w2 = T + 90, T + 210
r1, r2, r3 = T + 390, T + 510, T + 760
Note('Awl', Col(2), T + 55, "WRITE PATH: INGEST DOCUMENTS", 18, W)
Note('Arl', Col(2), T + 355, "READ PATH: ASK A QUESTION", 18, R)

Box('Au', Col(0), T + 380, BW, 80, "Employees\nchat window on the\nintranet site", EDGE)
Box('Ag', Col(1), w1, BW, r3 + H - w1, "APIM\n\nEntra ID login:\nuser + groups\n\ntoken quota\nper user", EDGE)
Arrow('Aa1', 'Au', 'Ag', "HTTPS / SSE", None, False, True)

# write: ingestion pipeline (only changed documents are re-embedded)
Box('Aup', Col(2), w1, BW, H, "Document Service\nPOST /documents\n(file + allowed groups)", SVC)
Box('Araw', Col(3), w1, BW, H, "Raw docs (Blob) +\nDocuments (SQL):\ngroups, version, hash", DATA)
Box('Aq', Col(4), w1, BW, H, "Queue: doc-changed\n(Service Bus)", ASYNC)
Box('Awk', Col(5), w1, BW, H, "Ingestion worker\n1 extract text 2 skip if\nunchanged 3 chunk", ASYNC)
Box('Aem', Col(6), w1, BW, H, "4 Embedding model\n(batches of 100\nchunks)", AI)
Box('Aix', Col(7), w1, BW, H, "5 Search index (AI\nSearch): vector + text\n+ groups; drop old", DATA)
Arrow('Aw1', 'Ag', 'Aup', '', W, False, True)
Arrow('Aw2', 'Aup', 'Araw', "save", W)
Arrow('Aw3', 'Araw', 'Aq', "event", W, True)
Arrow('Aw4', 'Aq', 'Awk', "consume", W, True)
Arrow('Aw5', 'Awk', 'Aem', "chunks", W, True)
Arrow('Aw6', 'Aem', 'Aix', "upsert", W, True)

Box('Aacl', Col(2), w2, BW, H, "Document Service\nPUT /documents/{id}/acl", SVC)
Box('Aacl2', Col(3), w2, BW, H, "Documents (SQL)\nnew groups", DATA)
Box('Aacl3', Col(4), w2, BW, H, "Patch 'groups' on the\ndoc's chunks only\n(no re-embedding)", ASYNC)
Arrow('Aw7', 'Ag', 'Aacl', '', W, False, True)
Arrow('Aw8', 'Aacl', 'Aacl2', "write", W)
Arrow('Aw9', 'Aacl2', 'Aacl3', "event", W, True)

# read: one question, start to finish
Box('Acs', Col(2), r1, BW, H, "Chat Service\nPOST /conversations/\n{id}/messages", SVC)
Box('Ahi', Col(3), r1, BW, H, "1 History (Cosmos)\nlast 10 turns +\nsummary", DATA)
Box('Arw', Col(4), r1, BW, H, "2 Rewrite follow-up\nas a full question\n(small model)", AI)
Box('Aqe', Col(5), r1, BW, H, "3 Embed the\nquestion", AI)
Box('Ase', Col(6), r1, BW, H, "4 Hybrid search +\nfilter: MY groups;\nrerank -> top 5", DATA)
Box('Allm', Col(7), r1, BW, H, "6 LLM answers ONLY\nfrom the 5 chunks,\ncites [1] [2]", AI)
Box('Aidk', Col(6), r2, BW, H, "5 Nothing relevant?\n'I don't know'\n(no LLM call)", GREY)
Box('Ast', Col(7), r2, BW, H, "7 Stream tokens to\nthe browser (SSE);\nsave turn + citations", EDGE)
Arrow('Ar1', 'Ag', 'Acs', '', R, False, True)
Arrow('Ar2', 'Acs', 'Ahi', "1", R)
Arrow('Ar3', 'Ahi', 'Arw', "2", R)
Arrow('Ar4', 'Arw', 'Aqe', "3", R)
Arrow('Ar5', 'Aqe', 'Ase', "4", R)
Arrow('Ar6', 'Ase', 'Allm', "6 prompt", R)
Arrow('Ar7', 'Ase', 'Aidk', "5 low score", RED, True)
Arrow('Ar8', 'Allm', 'Ast', "7", R)

Box('Ahl', Col(2), r3, BW, H, "Chat Service\nGET /conversations\n(cursor, 20 per page)", SVC)
Box('Ahl2', Col(3), r3, BW, H, "Conversations (Cosmos)\npk userId, TTL 90 days\nowner only", DATA)
Arrow('Ar9', 'Ag', 'Ahl', '', R, False, True)
Arrow('Ar10', 'Ahl', 'Ahl2', "", R)

Note('Asum', Col(4), r2 + 100, "PROMPT (~3k tokens): rules ('answer only from the sources, cite them,\nsay I don't know') + summary + last turns + the 5 chunks + the question.\nFirst token in ~1.5 s: rewrite 0.3 + embed 0.05 + search/rerank 0.3 + LLM 0.8.\nINGEST: unchanged file (same hash) -> skip; changed -> re-chunk + re-embed\nonly that doc; permission change -> patch metadata, no re-embedding.", 16, NOTE)

# =================== ROW B: ALL-HANDS SPIKE (only what differs; numbers = order) ===================
T = 960
Frame('fB', 0, T, 2400, 680)
Note('Bt', 20, T + 12, "B. ALL-HANDS SPIKE (new policy announced: 20k employees ask about it in 10 minutes) - numbers show the order", 24, RED)
w1, w2, w3 = T + 90, T + 210, T + 330
r1 = T + 520
Note('Bwl', Col(2), T + 55, "ASK A QUESTION (what changes)", 18, R)

Box('Bu', Col(0), T + 200, BW, 80, "20k employees\n(similar questions)", EDGE)
Box('Bg', Col(1), w1, BW, r1 + H - w1, "APIM\n\nper-user token\nlimit (no one user\nburns the quota)", EDGE)
Arrow('Ba1', 'Bu', 'Bg', "HTTPS / SSE", None, False, True)

Box('Bcs', Col(2), w1, BW, H, "1 Chat Service\n(rewrite + embed\nas row A)", SVC)
Box('Bsc', Col(3), w1, BW, H, "2 Semantic cache\n(Redis): similar question\n+ same groups?", DATA)
Box('Bhit', Col(4), w1, BW, H, "3 HIT: return the\ncached answer\n(no search, no LLM)", EDGE)
Box('Bmiss', Col(3), w2, BW, H, "4 MISS: search +\nprompt as row A", SVC)
Box('Brt', Col(4), w2, BW, H, "5 Model router: main\ndeployment; 429 ->\nsecond region", AI)
Box('Bput', Col(5), w2, BW, H, "6 Save the answer in\nthe cache (question\nvector, groups, doc v)", DATA)
Arrow('Bw1', 'Bg', 'Bcs', '', R, False, True)
Arrow('Bw2', 'Bcs', 'Bsc', "2", R)
Arrow('Bw3', 'Bsc', 'Bhit', "3 hit", R)
Arrow('Bw4', 'Bsc', 'Bmiss', "4 miss", R)
Arrow('Bw5', 'Bmiss', 'Brt', "5", R)
Arrow('Bw6', 'Brt', 'Bput', "6", R, True)

Box('Bpre', Col(2), w3, BW, H, "Before the all-hands:\ningest the new policy\n(write path as row A)", GREY)
Arrow('Bw7', 'Bg', 'Bpre', '', W, False, True)

Box('Bsame', Col(2), r1, BW, H, "History, citations,\nstreaming: as row A", GREY)
Arrow('Bw8', 'Bg', 'Bsame', '', R, False, True)

Note('Bnote', Col(3), w3 + 5, "WHY A SEMANTIC CACHE: thousands of people ask the same thing in\ndifferent words ('new leave policy?', 'how many days off now?').\nMatch by meaning (question vectors close enough), but ONLY for the\nsame permission groups and the same document versions, so a\ncached answer never leaks a document someone can't see, and a\nchanged document makes old answers stale automatically.\n\nQUOTA: the model has a tokens-per-minute limit. APIM caps each\nuser; a 429 from the main deployment spills to a second region.", 16, NOTE)
