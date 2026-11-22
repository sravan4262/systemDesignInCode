# M22 — LLM application fundamentals and RAG

> Plan: `Senior_Engineer_Interview_Plan.html#m22` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A tenant-scoped RAG service with citations and a stage-by-stage debugging guide.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
An LLM app is a normal distributed service with one slow, costly, non-deterministic dependency: the model. Sessions 1–3 make a single model call reliable (what goes in, what comes out, what fails). Sessions 4–6 add RAG: give the model the right company text at question time, with citations, and debug it when the answer is wrong. Session 7 builds it in .NET. The M25 design `ai-chat-company-docs` is the end goal these sessions feed.

### Sessions (in order)
1. **One LLM call.** Tokens, context window, sampling (temperature, top-p), model choice, pricing.
2. **Structured output and tool calling.** JSON schema output, function calling; malformed output, refusals, cut-off responses.
3. **Reliability.** Timeouts, bounded retries, streaming, provider rate limits (429), fallback models; versioning prompt + model + schema together.
4. **RAG ingestion.** Chunking, embedding models, vector index basics (exact vs. ANN, HNSW high level, recall vs. latency), metadata filters.
5. **Retrieval.** Hybrid search (BM25 + vectors), Reciprocal Rank Fusion, reranking, context assembly, citations.
6. **Debugging and freshness.** Stage-by-stage RAG debugging; re-indexing, deletions, embedding-model upgrades.
7. 🧪 **Build: deferred to the end of Phase 4** (learner, 2026-11-22). It becomes **build 1 of 3** (the RAG service); the scope lives in [the M25 README](../m25-production-ai/README.md#phase-4-builds-three-code-only-projects-built-in-order-learner-2026-11-22).

### Done when you can answer
- Why does a longer prompt cost more and respond slower, and what is temperature for?
- The model returns invalid JSON 2% of the time. What do you do?
- A provider returns 429 under load. Walk through your retry and fallback.
- The right document is in the index but the answer is wrong. Debug it stage by stage.
- Why hybrid search over vectors only, and what does RRF do?
- How do you switch embedding models without downtime?

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### One LLM call (tokens, cost, latency)
- **In plain words:** Text goes in, the model writes the answer back one token at a time (a token is about ¾ of a word). I pay for input and output tokens.
- **Problem it solves:** Knowing this stops me sending huge prompts. The model remembers nothing between calls, so a chat resends the whole history every turn, and by turn 40 it's slow and expensive.
- **Analogy:** A taxi meter: pay for distance in (prompt) and out (answer). The context window is the trunk size.
- **Diagram:** prompt (read all at once, fast) → model → answer (one token at a time, slow). Cost = input tokens × input price + output tokens × output price (output is ~4–5× more per token).
- **Variations / alternatives:** Big model for hard answers, small cheap model for rewrite / classify / route.
- **When not to use it:** Exact lookups or calculations, wrong answer not allowed with no human check, needs to be under 50 ms.
- **Quick-check answers:** Fix long chats with the last 10 turns + a summary, and prompt caching. A long prompt mostly delays the *first* word; the answer length drives the *total* wait, so stream it.

#### Temperature
- **In plain words:** A setting (0 to ~1), set once per call and used at every token: how strongly the model sticks to its top guess. Low = predictable, high = varied.
- **Problem it solves:** Facts need the same answer every time; creative tasks need variety. Temperature lets me pick per call.
- **Analogy:** Always ordering my favourite dish (0) vs. sometimes trying something else on the menu (1).
- **Diagram:** Each step: look at prompt + tokens so far → odds for the next token → pick using temperature → repeat. One unusual early pick changes everything after it.
- **Variations / alternatives:** Top-p (only pick from the top guesses adding up to p). Change one, not both. Reasoning models may ignore it.
- **When not to use it:** Don't raise it for facts, extraction, JSON, RAG answers or code (keep 0–0.2). Raise it (0.7–1) only for taglines and brainstorming.
- **Quick-check answers:** Refund-window bot → low (a fact from the doc). 5 subject lines → high (low gives near-copies). Temp 0 with slightly different runs → not broken: tiny server rounding flips near-tie picks, so test with many examples (evals).

#### Structured output
- **In plain words:** I give the model a JSON schema and it must reply in exactly that shape, so code can read it.
- **Problem it solves:** "Reply in JSON" in the prompt breaks 1–5% of the time (fences, chatty text, wrong types, cut off).
- **Analogy:** A form with labelled boxes instead of a free-text letter.
- **Diagram:** prompt + schema → model (temp 0) → validate → typed object. Invalid → retry once with the error → dead-letter. Refusal → its own case.
- **Variations / alternatives:** Prompt-only JSON < JSON mode (valid JSON, any shape) < strict schema (exact shape, use this). Tool calling uses the same mechanism. .NET: `GetResponseAsync<T>` in Microsoft.Extensions.AI.
- **When not to use it:** Answers that go to a person (stream plain text), huge nested schemas (split the call), cases a regex can do.
- **Quick-check answers:** The schema only checks the shape, not the truth: `amount: 1200` passes, so code checks it (is the number in the email? does it match the PO? is it in range?) and evals test it before shipping. **Always check the finish reason**: `length` means cut off (50 tokens for 20 products ≈ 300–400 needed). Raise the limit or ask for fewer per call. A cut-off plain-text answer can look fine, so check every time.

#### Tool calling
- **In plain words:** I write C# methods and pass them as tools in `ChatOptions`. The model only sees each tool's name, `[Description]` and parameters. From the user's message it picks a tool and its arguments and **asks** for it; **my code runs it** (`UseFunctionInvocation`) and sends the result back, then the model answers.
- **Problem it solves:** The model doesn't know my live data (order 123 shipped this morning). Without tools it says "I don't know" or makes something up.
- **Analogy:** A manager (LLM) asks an assistant (my code) to look something up. Only the assistant has the keys to the database.
- **Diagram:** question + tool list → model: "call GetRecentOrders()" → my code runs it → result → model: "call GetOrderStatus(124)" → result → final answer. Code: `src/M22.LlmApps/Controllers/OrderChatController.cs`.
- **Variations / alternatives:** **Fetch first**: if I already know what data is needed (the "Ask about this order" button on the order page), my code fetches it and puts it in the prompt. One LLM call, cheaper, predictable. Use **tool calling** for an open chat box where users can ask anything. RAG = fetch first, for documents. MCP (M24) = sharing tools across apps.
- **When not to use it:** When the data needed is always the same (fetch first), and with too many tools (the model picks wrong more often).
- **Quick-check answers:** My code runs the tool, not OpenAI, so permission checks happen on my server. **Every tool checks the owner itself**: without it in `GetRecentOrders`, alice sees bob's order ids, items and dates. Tools that change data (cancel, refund) only **request**; the user confirms with a separate endpoint, because the model can misread ("thinking about cancelling"), pick the wrong id, or be tricked by text in a document (prompt injection). Cap the loop (`MaximumIterationsPerRequest = 5`).

#### When the model call fails (timeouts, retries, 429, fallback)
- **In plain words:** Decide up front how long to wait, which errors to try again, and what to use when the main model is down.
- **Problem it solves:** No timeout → calls hang, requests pile up, the whole API slows. Immediate retries in a 429 storm → 2,000 requests become 8,000, so the overload gets worse.
- **Analogy:** A busy restaurant line: wait longer before each redial; "call back in 30 s" → wait 30 s; closed → call the other branch; "we don't serve that" → redialing won't help.
- **Diagram:** 200 → done. 429 → wait (Retry-After, or backoff 1→2→4 s + jitter), max 2 retries. 5xx / timeout → retry once. Still failing → fallback (other region, smaller model, cached answer) + circuit breaker. 400 / 401 → don't retry, fix it. All failed → "try again in a minute".
- **Variations / alternatives:** Fallback to the same model in another region (needs quota there), a smaller model (easy tasks), a semantic cache (repeated questions), or no AI (high-stakes answers). LLM specifics: rate limits are tokens per minute; set the timeout from the answer length (1,000 tokens ≈ 20 s, stream it); the OpenAI/Azure SDKs already retry, so don't stack my own retries on top (3 × 3 = 9).
- **When not to use it:** Don't retry 400/401, after streaming has started, or after a tool already changed data (idempotency). Don't silently fall back to a weaker model for high-stakes answers.
- **Quick-check answers:** 429 + Retry-After 20 → wait 20 s + random jitter (or use the fallback now, since a user is waiting), capped retries. 400 "prompt too long" → don't retry; shorten it (fewer turns + summary, fewer chunks) and count tokens before sending. Immediate retries during 429 only add load → backoff + jitter, cap, circuit breaker, or queue.

#### Streaming
- **In plain words:** Send the answer to the user piece by piece as the model writes it, instead of waiting for the whole answer.
- **Problem it solves:** A 300-token answer takes ~6 s. Without streaming the user sees a blank screen, thinks it's broken, clicks again (2 paid calls) or leaves. With streaming the first words show after ~0.5 s. Same total time, better experience.
- **Analogy:** Live TV captions vs. waiting for the transcript.
- **Diagram:** browser ◄SSE── API ◄stream── OpenAI. Tab closed → cancel the model call. Connection drops → keep the partial text, offer retry (no auto-retry). Finish reason `length` → "answer cut off".
- **Variations / alternatives:** SSE for chat (one-way, plain HTTP; .NET 10 `TypedResults.ServerSentEvents`), WebSockets for two-way (voice, collaboration), no streaming for background jobs and JSON. Tool calls arrive in pieces; `UseFunctionInvocation` puts them together.
- **When not to use it:** Background jobs, JSON for code, answers that must be checked before anyone sees them, very short answers.
- **Quick-check answers:** It improves the experience, not the total time. Tab closed → pass the request's `CancellationToken` so the model call is cancelled: stop paying for tokens nobody reads. Invoice JSON for code → don't stream; the code needs the full, valid JSON.

#### Versioning prompt + model + schema together
- **In plain words:** Prompt, model, output schema and settings (temperature) all change the output, so they're one bundle with one version number. Change any part → new version, tested before it goes live.
- **Problem it solves:** Someone edits the prompt and the provider updates the model in the same week; accuracy drops 98% → 85%, and nobody knows which change caused it or what to roll back to.
- **Analogy:** A versioned recipe card: ingredients (model), steps (prompt), plating (schema). Taste-test before serving; go back to the old card if customers complain.
- **Diagram:** `invoice-extract v3 = {prompt v3, gpt-4.1-mini-2025-04-14, Invoice v2, temp 0}` → edit → v4 → evals on 200 saved examples → 5% → 100%, or don't ship. Log the version on every call → roll back in one change.
- **Variations / alternatives:** Prompts in git (start here, reviewed in PRs) vs. a config store / prompt registry (no deploy needed, A/B tests, but easy to change without evals). Dated model snapshots. Schema changes like API versioning: adding a field is safe; renaming/removing needs a new version.
- **When not to use it:** Prototypes; a couple of prompts (a git file is enough). Don't pin snapshots forever: they get retired, so plan the upgrade with evals.
- **Quick-check answers:** Before Monday I need: versioned bundles, **the version logged on every call** (prompt or provider?), and **evals** (catch the drop before shipping, measure it after). A dated snapshot never changes; a plain name can be moved to a newer snapshot by the provider. Renaming `amount` → `totalAmount` breaks code reading `amount` (silently $0). Roll out with expand/contract: new bundle v5, code reads both, evals → canary → 100%, then drop the old field.

#### Embeddings
- **In plain words:** An embedding model turns text into a list of numbers (a vector, e.g. 1,536). Similar meaning → similar numbers, even with different words.
- **Problem it solves:** "How many days off do I get?" vs. "Annual leave entitlement is 25 days": no shared words, so keyword search misses it; embeddings put them close together.
- **Analogy:** A map of meanings: "days off", "annual leave", "vacation" are pins in the same neighbourhood.
- **Diagram:** text → vector; compare with cosine similarity (1 = same meaning, ~0 = unrelated). RAG = ingestion (chunk → embed → index) + query (embed question → search → top chunks → prompt → answer + citations).
- **Variations / alternatives:** Hosted embedding model (`text-embedding-3-small`, cheap), a larger model (better, more storage), self-hosted (data stays in-house). Keyword search (BM25) for exact words. .NET: `IEmbeddingGenerator` in Microsoft.Extensions.AI.
- **When not to use it:** Exact lookups (order id, error code, SKU), a few docs that fit in the prompt, structured questions (that's SQL).
- **How they're made:** text → tokens → each token's starting vector from a learned table → layers mix in context ("bank" in "river bank" vs. "bank account") → combined into ONE vector for the whole text. Trained on millions of same-meaning pairs (pulled together) and unrelated pairs (pushed apart). I use it as an API; I don't build it.
- **Vector = an array of numbers** (`float[]`). One vector per text; its length (dimensions, e.g. 1,536) is fixed by the model, even for a 2-token text. 1,536 × 4 bytes ≈ 6 KB per chunk → 1M chunks ≈ 6 GB.
- **Cosine similarity:** a score for the angle between two vectors (1 = same meaning, 0 = unrelated). Calculated, not configured like temperature. I configure the metric once on the index, top K per search, and an optional minimum score in my code. Cosine = which pages to read (search); temperature = how creative the answer is (writing). A 0.92 chunk goes in the prompt; 0.15 is unrelated.
- **Quick-check answers:** "PTO rules" vs. "Paid time off policy": keyword misses, embeddings match. Docs on model A + questions on model B → wrong chunks with **no error**, so always use the same model; switching models means re-embedding everything. "error E-4471" needs exact matching → keyword search; real systems combine both (hybrid).

#### Chunking
- **In plain words:** Split each document into chunks that each cover one idea; each chunk gets its own vector and is searched on its own.
- **Problem it solves:** A whole document blurs into one vector that matches nothing; a tiny chunk ("See section 4.") means nothing. Bad splits cut an idea in half or lose context ("This applies after 6 months": what does?).
- **Analogy:** Textbook → flashcards: one idea per card, chapter title at the top of each.
- **Diagram:** doc → split at headings (then by size, ~300–800 tokens) → each chunk = {text with heading prefix, vector, docId, section, page (for citations)} + ~50-token overlap with the next chunk.
- **Variations / alternatives:** Fixed size + overlap (simple); **by structure** (headings/sections, usual best default); semantic (split where the topic changes); parent–child (match on small chunks, send the bigger section); tables keep their header row, code split by function.
- **When not to use it:** Short items that are already one idea (FAQ pairs, tickets, product descriptions): embed each whole. A few docs that fit in the prompt: skip RAG. Don't start with semantic chunking; measure first.
- **Quick-check answers:** Lost context → add the heading to the chunk text, and split by structure so the heading stays with its text (or parent–child). Overlap repeats the end of one chunk at the start of the next, so an idea cut at the edge appears whole in at least one chunk (chunks are searched separately). 200 FAQ pairs → one chunk per pair (200 vectors), NOT one vector for all; or skip RAG and send all ~20k tokens (simpler, but paid on every call).

#### Vector index basics
- **In plain words:** A separate structure (like a non-clustered index, but for similarity) that finds the vectors closest to the question's vector without comparing against all of them.
- **Problem it solves:** Exact search = compare with every vector: 1M × 1,536 ≈ 1.5 billion operations per question, the vector version of a full table scan.
- **Analogy:** Nearest coffee shops: highway to the city → main roads → side streets, instead of measuring every shop in the country.
- **Diagram:** HNSW = layers of a neighbour graph. Start at the top (few points, long jumps), always move to the neighbour closest to the question, drop a layer, repeat → top 5 after checking a few thousand points. It walks a graph; it doesn't scan or sort (1,536 dimensions have no single order).
- **Variations / alternatives:** Exact KNN (100% recall, slow; fine ≤ ~50k vectors or after a strong filter). HNSW = ANN (Approximate Nearest Neighbour), the default, lives in RAM. Recall = of the true top 5, how many came back (4/5 = 80%). Knob `efSearch`: higher → better recall, slower. Stores: Azure AI Search (hybrid + filters managed), pgvector (already on Postgres), Cosmos DB vector search, Qdrant/Pinecone.
- **When not to use it:** Small data (just scan), exact id/code lookups (normal index), tuning the knob before measuring recall on my data.
- **Quick-check answers:** Comparing with every vector is too many operations per question; fine when the data is small or filtered down. 4 of 5 = 80% recall; raise `efSearch` → slower search, more CPU. Postgres + 200k chunks → pgvector: no new system, join/filter with my tables in one query (`WHERE tenant_id = @t ORDER BY embedding <=> @q LIMIT 5`); move to Azure AI Search for strong hybrid + reranking or many millions of chunks.

#### Metadata filters
- **In plain words:** Each chunk row stores labels next to its text and embedding (tenant, allowed groups, doc type, date, citation fields). At search time a `WHERE` on those labels limits the search to chunks the user may see.
- **Problem it solves:** Without filters, alice (Sales) asks about salary bands and gets the HR-only chunk (score 0.94); or tenant A gets an answer from tenant B's documents.
- **Analogy:** A library where the librarian only searches the rooms your badge opens.
- **Diagram:** one row per chunk: `content, embedding, tenant_id, allowed_groups, doc_id, section, page`. Ingestion copies the document's permissions onto every chunk. Query: `WHERE tenant_id = @t AND allowed_groups && @groups ORDER BY embedding <=> @q LIMIT 5`, one SQL query (don't pull rows into C# and compare there). Permission change → `UPDATE allowed_groups`, no re-embedding.
- **Variations / alternatives:** Filter fields in one shared index (default) vs. an index/database per tenant (strict isolation, more cost). Relevance filters (doc type, date). **pgvector catch:** HNSW collects ~`ef_search` candidates and then applies the `WHERE`, so it can return fewer than 5 → `SET hnsw.iterative_scan = relaxed_order`; a small tenant may get a B-tree + exact scan (100% recall); partition by tenant for big ones; check with `EXPLAIN`.
- **When not to use it:** Public data (no security filter). Too many relevance filters over-narrow. Don't store per-user ids in every chunk; store group ids.
- **Quick-check answers:** A prompt rule ("don't reveal HR data") is just text the model can ignore or be talked out of, and the data is already in the prompt; **security is enforced before data reaches the model**. Post-filter (top 5, then drop forbidden) can leave 1 chunk → pre-filter, then nearest neighbours. Groups come from the **JWT** (signed by the identity provider), never the question (anyone can type "I'm in HR"); with too many groups, look them up from Graph and cache.

#### Hybrid search (keyword + vectors)
- **In plain words:** Run two searches on the same question, keyword (BM25) and vector (meaning), then merge them into one list.
- **Problem it solves:** Vectors miss exact codes and internal names ("error E-4471", "SKU-88213", "Project Falcon"); keywords miss synonyms ("PTO" vs. "paid time off"). Users ask both kinds.
- **Analogy:** Two librarians: one uses the index at the back of the book (exact words), one understands what you mean.
- **Diagram:** question → keyword search (top 20) + vector search (top 20) → merge (RRF) → top 5. One chunk row = `content` (my chunking code), `embedding` (my code calls the embedding model), `content_tsv` (Postgres builds it from `content`: stop words dropped, words stemmed, positions kept; GIN index). There's no "hybrid column": hybrid = using both search columns at query time, with the same tenant/group filters in both halves. One SQL statement, two CTEs. Azure AI Search: send text + vector in one request.
- **BM25:** keyword scoring that rewards rare words ("E-4471" in 2 of 1,000 chunks ≫ "when" in all of them), gives less credit for each repeat, and prefers short focused chunks. Postgres `ts_rank` is simpler than true BM25 (no rare-word weighting); use an extension or Azure AI Search for real BM25.
- **When not to use it:** Only exact ids → a database lookup (analyzers may even split "TCK-55102"). Only natural language → vectors may be enough. Always test on my own questions.
- **Quick-check answers:** "PTO rules" → vector half. "SKU-88213" → keyword half (a code has no meaning for the embedding model). "Reset my password" vs. an article saying "recover your login credentials" → keyword finds nothing, vector has it at #2, so it still ends up near the top.

#### Reciprocal Rank Fusion (RRF)
- **In plain words:** Merge two ranked lists using positions, not scores: each chunk gets `1 / (60 + rank)` from each list it's in, added up; chunks high in both lists win.
- **Problem it solves:** BM25 (e.g. 12.4) and cosine (e.g. 0.87) are on different scales; adding them lets one search swamp the other.
- **Analogy:** Two judges scoring out of 100 and out of 10: compare their rankings, not their numbers.
- **Diagram:** rank = position after sorting by that search's score (`ROW_NUMBER()`), computed per question, never stored. X = #1 in keyword only → 1/61 = 0.0164; Y = #4 in both → 2/64 = 0.0313 → Y wins. k = 60 keeps the gap between #1 and #5 small so one list can't dominate.
- **Variations / alternatives:** Weighted RRF (tune with evals), normalise scores and add (fragile), a reranker after RRF.
- **When not to use it:** One search only; as an "I don't know" signal (the #1 result always scores ~0.033 even if it's junk); when one list is much worse (weight or drop it).
- **Quick-check answers:** Different scales → use positions. "Dress code on Mars?" → the RRF score is high even for junk, and keyword search would match the real dress-code policy, so the "I don't know" check uses the reranker score or the raw cosine of the best chunk, with a threshold tested on my data.

#### Reranking
- **In plain words:** After hybrid search + RRF give the top ~20, a reranker model reads the question and each chunk TOGETHER and scores how well the chunk answers it; keep the top 5.
- **Problem it solves:** Search is fast but shallow: RRF ranks only by position, and embeddings were made separately. The answer chunk ("Up to 5 unused days can be carried over") sits at #7 behind chunks that are merely about leave, and only 5 go into the prompt.
- **Analogy:** A keyword filter shortlists 20 CVs from 1,000; a person reads those 20 against the job and picks 5.
- **Diagram:** 1M chunks → hybrid + RRF (~20 ms) → top 20 → reranker (~100–300 ms) → top 5 → LLM. Best score below a tested threshold → "I don't know" (no LLM call).
- **Three different models in RAG:** embedding model (one text → vector; a bi-encoder), **reranker** (question + chunk → relevance score; a cross-encoder), LLM (writes the answer). Not the LLM for ranking: slow, ~10k tokens per question, less consistent.
- **Where it runs:** Postgres has none: a separate API call from my C# code after the SQL (LIMIT 20 → rerank → top 5). Azure AI Search: built in (`QueryType = Semantic`, `RerankerScore` 0–4), so hybrid + RRF + rerank in one request.
- **When not to use it:** Latency-critical search (autocomplete), search already good (answer nearly always in the top 3), tiny lists.
- **Quick-check answers:** Rerank all 1M = one model run per chunk (~80 min per question), so search narrows first. More accurate because it reads question + chunk together. "Dress code on Mars": RRF #1 still scores ~0.033, but the reranker scores it ~0.05 → "I don't know".

#### Context assembly and citations
- **In plain words:** Build the prompt from the top chunks: number them, drop duplicates, stay within a token budget, tell the model to answer only from them and cite `[n]`, then check the citations in code.
- **Problem it solves:** Raw pasted chunks → the model mixes in its own knowledge, nobody knows where the answer came from, overlapping chunks waste tokens, old and new policy versions contradict, and document text can hijack the model (prompt injection).
- **Analogy:** A lawyer's brief with numbered exhibits; claims without an exhibit are thrown out.
- **Diagram:** system rules + `<source id="1" doc section updated>...</source>` ... + question → "Yes, up to 5 days [1]..." → code checks every `[n]` and turns it into a link.
- **Assembly rules:** best chunk first ("lost in the middle"); dedupe / merge neighbours; newest version wins; ~3k-token budget; chunk text inside tags, marked as data.
- **How citations are found:** NOT by parsing chunks per query. Source info (doc, section, page, url) is stored with each chunk at ingestion; search returns the top 5 rows with it; my code numbers them 1–5 for this question; the model writes only `[n]`; a regex over the short answer (or structured output `{ answer, citations }`) maps `[n]` back to row n's metadata.
- **Variations / alternatives:** Inline `[n]` (common), structured output, a sources list, exact quotes.
- **When not to use it:** Small talk, tool answers (nothing to cite), one tiny document. Don't trust citations blindly: check high-stakes claims against the cited chunk (evals).
- **Quick-check answers:** Best chunk first because models attend most to the start and end. Invented `[3]` (only 1–2 sent) → the prompt asks, the **code checks**: drop it, treat that sentence as unsupported (remove / regenerate / mark it), log it for evals. Injection in a chunk → citations don't help (the bad chunk is a source); layers instead: mark chunk text as data, no secrets in the system prompt, least-privilege tools + confirmations, filter at ingestion, check the output. Assume it sometimes works and limit the damage.

#### Debugging RAG stage by stage
- **In plain words:** When the right document exists but the answer is wrong, follow the right chunk through the stages in order and fix the FIRST stage where it went missing, not the prompt.
- **Problem it solves:** People tweak the prompt, but the chunk was never indexed, was filtered out, or ranked #6 when only 5 were sent. A prompt can't fix a chunk the model never saw.
- **Analogy:** A lost parcel: check the tracking scans to see which depot lost it.
- **Diagram:** 1 Ingestion (is the text in `chunks`?) → 2 Filter (can THIS user see it?) → 3 Retrieval (in the hybrid top 20?) → 4 Rerank (in the top 5?) → 5 Assembly (in the final prompt? budget/dedupe/version) → 6 Generation (in the prompt but answered wrong: finish reason, conflicting sources, instructions, model). Needs a **trace per question**: user, tenant, groups, keyword/vector/RRF ids, rerank scores, prompt source ids, tokens, prompt version, model, answer, citations.
- **Variations / alternatives:** A golden set (50–200 questions + expected chunk + answer) measured per stage: retrieval recall@20, recall@5 after rerank, faithfulness (claims supported by sources), answer correctness. Run on every change (evals, M24).
- **When not to use it:** No dashboards before a golden set; don't keep sensitive traces forever (retention, access control); a one-off bug only needs the trace.
- **Quick-check answers:** HR-only chunk hidden from a Sales user → filter stage, working correctly (unless the doc was tagged wrong at ingestion: then it's a data bug). Chunk in the prompt but wrong answer → stage 6: check finish reason, conflicting sources, instructions, then change the prompt or model. Prompt tweaks come last because the prompt is the last stage.

#### Freshness (edits, deletions, permission changes, embedding upgrades)
- **In plain words:** Keep the index in step with the real documents: re-chunk and re-embed only what changed, remove deleted content fast, update permissions in place, and rebuild alongside for a new embedding model.
- **Problem it solves:** The index is a copy, and copies go stale: "25 days [1]" after HR changed it to 28 (wrong but cited); deleted or revoked documents still searchable; a new embedding model on one side → junk with no error.
- **Analogy:** A library catalogue: new edition replaces the card, a withdrawn book's card is pulled at once, a new catalogue system is built next to the old one before switching.
- **Diagram:** detect changes (source events + a scheduled scan; content hash per doc/chunk to skip unchanged) → edit = `BEGIN; DELETE chunks of doc; INSERT new chunks; UPDATE documents version/hash; COMMIT` (embeddings computed before) → delete = `DELETE ... WHERE doc_id` within ~1 min + clear the semantic cache → permissions = `UPDATE allowed_groups` (no re-embedding) → model upgrade = add `embedding_v2` + index, backfill in batches, evals v1 vs. v2, switch the query side, keep v1 for rollback, then drop.
- **Variations / alternatives:** Incremental (events + hashes) vs. a full nightly rebuild (small corpora); Azure AI Search indexers with change and deletion detection. NFR: deletions / permissions ~1 min, edits / new docs ~15 min.
- **When not to use it:** Small corpora → just rebuild nightly; don't re-embed millions of chunks for a tiny gain without evals; deletions are never "eventually" (security, GDPR).
- **Quick-check answers:** One transaction because separate steps leave a window where search sees nothing (delete first) or both versions (insert first), and a crash in between leaves the doc missing or doubled. Permission change → `UPDATE allowed_groups`, no re-embedding, fast, clear cached answers. Switch the query side only after ALL chunks are re-embedded: two models' vectors can't be compared, so a half-migrated index silently misses half the corpus; run evals v1 vs. v2 first and keep v1 for rollback.

#### <Topic name>
- **In plain words:**
- **Problem it solves:**
- **Analogy:**
- **Diagram:** (ASCII/Mermaid or a link to `designs/`)
- **Variations / alternatives:**
- **When not to use it:**
- **Quick-check answers:**

### "Done when" questions: graded 2026-11-22 (6.3 / 10)
| # | Question | Score | Gap to fix |
|---|---|---|---|
| 1 | Longer prompt cost/latency, temperature | 4 | Long prompt = more input tokens billed + longer time to first token (the model reads the whole prompt first); total time is driven by the answer length (one token at a time, ~4–5× the price). Not "tokens finding chunks" (search happens before the model) |
| 2 | 2% invalid / cut-off JSON | 4 | Strict schema (structured output), check finish reason every call, validate → retry once with the error → dead-letter, business checks, temperature 0 |
| 3 | 429 under load | 5 | Retry-After / backoff + jitter, ~2 retries; fallback region/deployment or smaller model; circuit breaker = stop calling for ~30 s (not a queue); per-tenant token budgets; don't retry 400/401 |
| 4 | Right doc, wrong answer | 9 | Mention the per-question trace that shows each stage |
| 5 | Hybrid + RRF | 7.5 | Vectors = synonyms, keywords = exact codes; RRF merges by position (`1/(60+rank)`) because the scores are on different scales |
| 6 | Embedding upgrade | 8 | Evals v1 vs. v2 before switching, keep v1 for rollback; content isn't stale during the backfill, only search quality differs |

**Retry later:** Q1–Q3 (Sessions 1–3).

### Problem
_What real engineering problem does this concept solve?_

### Internals
_How does it work? What guarantees does it provide?_

### Implementation
_What did I build? Code paths, schema, configuration, tests._

### Failure
_What did I deliberately break? What happened? (Tick the matching items in the plan's section 4.)_

### Recovery
_How does the system detect, recover, or reconcile the failure?_

### Trade-offs
_What alternatives exist? Why choose this design?_

### Production
_What should be monitored? What are the operational risks?_

### Interview
_Follow-up questions an interviewer could ask, with my short answers._

### My notes
_Which Apple Notes did this update or correct?_
