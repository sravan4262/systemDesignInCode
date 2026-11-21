# Case 12: AI chat over company documents (RAG)

Diagram: [case-12-ai-chat.excalidraw](case-12-ai-chat.excalidraw), in two rows: **A. Normal day** (write path = **ingest documents**, read path = **ask a question**, every step numbered) and **B. All-hands spike** (20k employees ask about one new policy in 10 minutes; only what differs). Employee → APIM (tall box) → paths. Purple boxes are AI model calls. To view or edit it, open it at excalidraw.com (File → Open) or with the VS Code Excalidraw extension.

**How this case differs from the others:** it isn't about consistency or availability. It's about building it **efficiently**: good answers, low cost per question, fast first word, and never showing an employee a document they can't see.

## Prompt (what the interviewer says)
> "Add an AI assistant to our company intranet. Employees ask questions in a chat window and get answers from our internal documents."

## Functional requirements (in scope; learner's list, 2026-11-21)
1. **Chat window** on the intranet site for **logged-in employees**: ask a question, the answer **streams** back word by word, with **citations** (which document, which section).
2. **Answers only from company documents** (policies, manuals, PDFs), and only from documents **this employee is allowed to see**. Nothing relevant → "I don't know" instead of guessing.
3. **Document ingestion:** new or changed documents (with their allowed groups) become searchable within minutes.
4. **Chat history:** a list of past conversations, reopen one, delete one; follow-up questions work ("and for contractors?"). Kept **90 days**, visible **only to the owner**.
5. **How much the AI remembers:** the last ~10 turns word for word + a running summary of anything older.

**Out of scope (say this out loud):** structured data (databases, "my payslip"), taking actions ("book leave"), handing off to a human, voice, fine-tuning, public / anonymous users.

## What "efficient" means here (instead of the usual non-functional table)
| Goal | Target | Main levers |
|---|---|---|
| **Answer quality** | Grounded in the documents, cited, "I don't know" when unsure | Good chunking, hybrid search + rerank, strict prompt rules, evaluation set |
| **Latency** | First word < ~1.5 s; full answer in a few seconds | Streaming, small model for side tasks, top-5 chunks only, parallel steps |
| **Cost per question** | Cents or less | Token budget (history + 5 chunks), small vs. large model routing, semantic cache |
| **Freshness** | A changed document is answerable within minutes | Event-driven ingestion, re-embed only what changed |
| **Security** | Never answer from a document the user can't see | Allowed groups stored on every chunk; search **filters** by the user's groups |

## Capacity estimate (high level: tokens and money)
**Facts:** 20k employees; 5 questions per employee per day; prompt ≈ 3k tokens in (rules + history + 5 chunks) and ≈ 300 tokens out; 100k documents × ~5k tokens each; ~1% of documents change per day; 1 day ≈ 10⁵ s. Prices are rough list prices, for order of magnitude only.

| What | Math | Result | So… |
|---|---|---|---|
| Questions | 20k × 5 | **100k/day ≈ 1/s avg, ~10/s peak** | Tiny request rate; the cost is **tokens**, not servers |
| LLM tokens | 100k × 3.3k | **~330M tokens/day** | Small model ≈ tens of $/day; large model ≈ ~$1k/day → route and cache |
| Document corpus | 100k × 5k tokens | **500M tokens → ~1M chunks of ~500 tokens** | Embedding everything once costs ~$10; the index holds ~6 GB of vectors |
| Daily re-ingestion | 1% of 100k | **~1k docs/day** | Only changed docs are re-embedded (content hash) |
| All-hands spike | 20k questions in 10 min | **~35/s, mostly the same question** | Hits the model's tokens-per-minute quota → **semantic cache** + per-user limits |

**What the numbers tell you:** servers are not the problem; **tokens** are. Efficiency comes from sending fewer, better tokens (good retrieval, small prompts), using the cheapest model that works for each step, and not calling the model at all when a good answer already exists.

## API
| Endpoint | Request | Response | Notes |
|---|---|---|---|
| `POST /documents` | file + `{ title, allowedGroups[] }` | `202 { docId, status: ingesting }` | Admin / document owners |
| `PUT /documents/{id}/acl` | `{ allowedGroups[] }` | `204` | Patches chunk metadata, no re-embedding |
| `DELETE /documents/{id}` | — | `204` | Removes its chunks from the index |
| `POST /conversations` | — | `201 { conversationId }` | — |
| `POST /conversations/{id}/messages` | `{ text }` | **SSE stream**: `token` events, then `done { messageId, citations: [{ docId, title, section }] }` | The main call |
| `GET /conversations?cursor=` | — | `200 { items: [{ id, title, updatedAt }], nextCursor }` | Sidebar, 20 per page |
| `GET /conversations/{id}/messages?before=&limit=50` | — | `200 { messages[], nextCursor }` | Open an old chat |
| `DELETE /conversations/{id}` | — | `204` | Owner only |
| `POST /messages/{id}/feedback` | `{ helpful: bool, comment? }` | `204` | Feeds the evaluation set |

## High-level design (see the diagram)
**Services and stores:**
| Component | What it does | Technology |
|---|---|---|
| APIM | Entra ID login (user + **groups** in the token), per-user token quota | Azure API Management |
| Document Service | Upload, change permissions, delete | **Blob** (raw files) + **Documents (SQL)**: allowed groups, version, content hash |
| Ingestion worker | Extract text → skip if unchanged → chunk → embed → upsert | Service Bus queue; **Document Intelligence** for PDFs / scans; **embedding model** (e.g. text-embedding-3-small) |
| Search index | Chunks with **vector + text + metadata** (docId, title, section, **allowedGroups**, version) | **Azure AI Search** (hybrid search + semantic reranker) |
| Chat Service | Runs one question end to end and streams the answer | Small model for rewrite / summary, larger model for the answer (Azure OpenAI) |
| Conversations | Conversation list, messages, citations, running summary | **Cosmos DB**, pk `userId`, TTL 90 days |
| Semantic cache (row B) | Reuse answers to questions with the same meaning, same permissions, same document versions | **Redis** with vector search |

### Write path: ingest a document
1. Upload → raw file to Blob, metadata (allowed groups, version, **content hash**) to SQL → event on the queue.
2. Worker **extracts** text (Document Intelligence for PDFs, tables and scans).
3. **Same hash as last time → skip** (nothing to do; the most common case for re-uploads).
4. **Chunk** by headings into ~500-token pieces with a small overlap; each chunk keeps its doc title and section path ("HR Policies > Leave > Contractors").
5. **Embed** chunks in batches of ~100 (fewer calls, cheaper).
6. **Upsert** into the index with the allowed groups and version; **delete** the previous version's chunks.
- **Permission change:** patch `allowedGroups` on that doc's chunks; no re-embedding (the text didn't change).

### Read path: ask a question (one round trip, streamed)
1. **History:** load the last ~10 turns + the running summary (Cosmos).
2. **Rewrite** a follow-up into a standalone question with a **small, cheap model**: "and for contractors?" → "What is the parental leave policy for contractors?"
3. **Embed** the rewritten question.
4. **Hybrid search** (vector + keyword) with a **filter: allowedGroups contains any of my groups** → top ~20 → **rerank** → **top 5** chunks.
5. **Nothing relevant** (best score below a threshold) → answer "I don't know, try asking HR" **without calling the LLM**.
6. **Prompt** (~3k tokens): rules ("answer only from the sources below, cite them as [1], [2], say you don't know otherwise") + summary + recent turns + the 5 chunks + the question → the **answer model**.
7. **Stream** tokens to the browser over SSE; then save the turn with its citations; every ~10 turns, refresh the running summary (small model, in the background).

**Latency budget to the first word (~1.5 s):** rewrite ~0.3 s + embed ~0.05 s + search and rerank ~0.3 s + model first token ~0.8 s. Load history in parallel with the rewrite where possible.

### Efficiency levers (what to say when the interviewer asks "how would you make it cheaper / faster / better?")
| Lever | Saves | How |
|---|---|---|
| Re-embed only changed docs | Ingestion cost | Content hash per document |
| Good chunks (by heading, ~500 tokens) | Quality + prompt size | Each chunk answers one thing; fewer chunks needed |
| Hybrid search + rerank | Quality + prompt size | Keywords catch exact terms ("Form W-4"), vectors catch meaning; rerank picks the best 5 |
| Token budget | Cost + latency | Last 10 turns + summary; top 5 chunks; cap the answer length |
| Model routing | Cost | Small model for rewrite and summaries; large model only for the final answer (or small for simple questions) |
| "I don't know" without the LLM | Cost + trust | Skip the model when retrieval finds nothing relevant |
| Semantic cache | Cost + latency at spikes | Row B |
| Streaming | Perceived latency | The first word shows in ~1.5 s even if the full answer takes 5 s |
| Evaluation set | Quality over time | 200 real questions with expected sources; run on every change to chunking, prompts or models; thumbs-up/down feedback adds to it |

## All-hands spike (row B): what changes, in order
1. The Chat Service rewrites and embeds the question as in row A.
2. **Semantic cache lookup** (Redis vector search): is there a cached answer to a question **close in meaning**, for the **same permission groups**, built from the **same document versions**?
3. **Hit** → return the cached answer (with its citations): no search, no LLM call.
4. **Miss** → search and build the prompt as in row A.
5. **Model router:** call the main model deployment; on `429` (tokens-per-minute quota hit), spill over to a second deployment / region instead of failing.
6. **Save** the answer in the cache, keyed by the question vector + the groups + the document versions used.
- **Before the announcement:** ingest the new policy first, so the first question already finds it.
- **APIM** caps tokens per user, so a few people (or a script) can't use up the shared quota.

**Why the cache key includes groups and versions:** a cached answer must never reach someone who can't see its source document, and an answer built from v3 of a policy must not be served after v4 is published.

## Which components each path touches
| Requirement | Path | Normal day (row A) | All-hands spike (row B) |
|---|---|---|---|
| Ingest a document | Write | Document Service → Blob + SQL → queue → worker (extract, skip if unchanged, chunk) → embed (batches) → index (vector + text + groups) | Same, done **before** the announcement |
| Change permissions | Write | SQL → patch groups on the chunks (no re-embedding) | Same |
| Ask a question | Read | History → rewrite (small model) → embed → hybrid search filtered by my groups → rerank top 5 → answer model → stream → save | + semantic cache first (hit → done); model router with spill-over on 429 |
| "I don't know" | Read | Low search score → reply without the LLM | Same |
| Chat history | Read | Conversations (Cosmos) by userId, cursor pages, TTL 90 days | Same |

## Deep dives to drive (learner leads, interviewer pushes)
1. **Chunking:** fixed size vs. by headings vs. by meaning; chunk size vs. answer quality; why the section path in each chunk helps both search and citations.
2. **Permissions:** filter at search time (security trimming) vs. checking after retrieval (too late: the model may have seen it). What if a user's groups change: the token refresh carries new groups; cached answers are keyed by groups.
3. **Hybrid search and reranking:** why vectors alone miss exact terms (policy numbers, product codes), and why keywords alone miss paraphrases.
4. **Hallucinations:** strict prompt rules, citations required, "I don't know" threshold, and a groundedness check in the evaluation set.
5. **Conversation memory:** last N turns vs. a running summary vs. retrieving relevant old turns; what it costs per question.
6. **Prompt injection:** a document contains "ignore previous instructions…". Treat retrieved text as data (clear delimiters), keep the rules in the system message, never let documents trigger actions.
7. **Evaluating changes:** how you'd know a new chunk size or a cheaper model didn't make answers worse (an evaluation set + user feedback, compared before release).

## Interview follow-ups (answer these after the design)
- An employee asks "what's the bonus policy for executives?" and isn't in the executives group. Walk through exactly why they don't get that answer, including from the cache.
- Cost doubled this month. Which three levers do you pull first, and what do you measure to make sure answers didn't get worse?
- A policy PDF is updated at 10:00. When does the chat start answering from the new version, and what happens to cached answers built from the old one?
