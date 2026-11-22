using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.Rag.Query;

namespace Phase4.Rag.Controllers;

// The query path: question → hybrid search (top 20) → rerank (top 5) → prompt → LLM → checked citations.
// Every stage writes into a RagTrace; the answer returns its traceId so a bad answer can be debugged later.
[ApiController]
[Route("ask")]
public class AskController(
    HybridSearch search,
    Reranker reranker,
    PromptBuilder prompts,
    IChatClient chat,
    CitationChecker citations,
    TraceStore traces,
    IConfiguration config) : ControllerBase
{
    public record AskRequest(string Question);

    // POST /ask   headers X-Tenant-Id: acme, X-User-Groups: all-staff,hr   body { "question": "..." }
    [HttpPost]
    public async Task<IActionResult> Ask(AskRequest req,
        [FromHeader(Name = "X-Tenant-Id")] string tenantId,
        [FromHeader(Name = "X-User-Groups")] string groups,
        CancellationToken ct)
    {
        // real app: tenant + groups from the JWT claims. Never from the body or the question.
        var userGroups = groups.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var trace = new RagTrace { TenantId = tenantId, Question = req.Question };

        // 1. Find candidates the user is allowed to see (permission filter is inside the SQL).
        List<SearchHit> candidates;
        using (trace.Time("search"))
            candidates = await search.SearchAsync(req.Question, tenantId, userGroups, topK: 20, trace, ct);

        // 2. Rerank: keep the best 5, and only those that really answer the question.
        List<RankedHit> reranked;
        using (trace.Time("rerank"))
            reranked = await reranker.RerankAsync(req.Question, candidates, topN: 5, ct);
        trace.RerankScores = reranked.Select(r => new RagTrace.ScoredChunk(r.Hit.ChunkId, r.Score)).ToList();

        var ranked = reranked.Where(r => r.Score >= Reranker.MinScore).ToList();
        if (ranked.Count == 0)
        {
            trace.Answer = "I don't know: none of the documents you can see cover this.";   // no LLM call
            await traces.SaveAsync(trace, ct);
            return Ok(new { answer = trace.Answer, citations = Array.Empty<Citation>(), traceId = trace.TraceId });
        }

        // 3. Build the prompt, 4. call the LLM (temperature 0: same sources → same answer).
        var prompt = prompts.Build(req.Question, ranked);
        trace.PromptVersion = PromptBuilder.Version;
        trace.PromptSourceIds = prompt.Sources.Select(s => s.ChunkId).ToList();

        ChatResponse response;
        using (trace.Time("llm"))
            response = await chat.GetResponseAsync(prompt.Messages, new ChatOptions { Temperature = 0 }, ct);
        trace.Model = response.ModelId ?? config["OpenAI:Model"] ?? "";
        trace.SetUsage(response.Usage?.InputTokenCount ?? 0, response.Usage?.CachedInputTokenCount ?? 0, response.Usage?.OutputTokenCount ?? 0);

        // 5. Keep only citations that point at a source we really sent.
        var (answer, cited) = citations.Check(response.Text, prompt.Sources);
        (trace.RawAnswer, trace.Answer) = (response.Text, answer);

        await traces.SaveAsync(trace, ct);
        return Ok(new { answer, citations = cited, traceId = trace.TraceId });
    }

    // GET /ask/3f2a.../trace   "why was this answer wrong?" → every stage of that one question
    [HttpGet("{traceId}/trace")]
    public async Task<IActionResult> GetTrace(string traceId, [FromHeader(Name = "X-Tenant-Id")] string tenantId, CancellationToken ct)
    {
        // real app: only support / admin roles may read traces (they contain the user's question and answer)
        var json = await traces.GetAsync(tenantId, traceId, ct);
        return json is null ? NotFound() : Content(json, "application/json");
    }
}
