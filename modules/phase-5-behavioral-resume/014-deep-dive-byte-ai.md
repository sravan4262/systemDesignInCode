# Deep dive 2 (AI): Byte AI hybrid search and "For You" feed

A 20-minute walkthrough of the AI side of Byte AI. The 3-minute STAR version is ST19 in [004-ownership-impact](004-ownership-impact.md). Back to [001-start-here](001-start-here.md).

**Done when:** you can give it in 20 minutes with a diagram, and it survives five levels of "why?".

## Structure (fill every ❓)
| Part | Time | What to cover | Known (resume) |
|---|---|---|---|
| 1. Product and scale | 1 min | What Byte AI is, who uses it | Tech-focused social platform, iOS + web, ~50 DAU (state it plainly) |
| 2. Architecture | 3 min | Diagram: SwiftUI iOS + Next.js web → .NET 9 API → PostgreSQL/Supabase (+ pgvector); SignalR chat; APNs | All on the resume. ❓ draw it |
| 3. Hybrid search | 5 min | Embeddings, the vector index, full-text, fusion | Self-hosted ONNX embeddings (nomic-embed-text) + pgvector + PostgreSQL full-text, fused with Reciprocal Rank Fusion. ❓ index type (HNSW?), the k in RRF, how you judged quality |
| 4. "For You" feed | 4 min | Interest embeddings and ranking | Ranks posts by cosine similarity to per-user interest embeddings. ❓ how the interest vector is built and updated, cold start, freshness, diversity |
| 5. Gemini pipeline | 3 min | Tagging, quality scoring, two-layer moderation | ❓ what the two layers are; failure handling; cost per post |
| 6. Release and ops | 2 min | Blue-green on Azure; CI/CD for .NET, Vercel, TestFlight | ❓ how migrations work with blue-green; monitoring |
| 7. What I'd redo | 2 min | Honest trade-offs | ❓ |

## "Why" ladders to prepare
- Why self-host embeddings? → why ONNX? → why nomic-embed-text? → what does it cost vs. an API? → what if you need to change models (re-embed everything: M22)?
- Why hybrid and not vector only? → why RRF and not score blending? → how did you measure that it's better? → what queries still fail? → how would you fix them (reranker)?
- Why cosine similarity for the feed? → how does the interest vector change after a like? → how do you avoid showing only one topic? → what about new users? → what at 1M users (precompute vs. compute on read)?

## Follow-ups (from the plan)
- **Scale 10× / 1000×:** pgvector limits, when to move to a dedicated vector store, SignalR backplane, precomputed feeds.
- **Cost:** Gemini calls per post, embedding compute, hosting.
- **Monitoring:** search latency, moderation false positives, feed engagement.
- **Security:** prompt injection in user posts sent to Gemini; abusive content that slips past moderation.

Related concepts: M22 (embeddings, HNSW, hybrid search, RRF), M25 (guardrails, cost), build 1 `Phase4.Rag` (the same hybrid search, in code).
