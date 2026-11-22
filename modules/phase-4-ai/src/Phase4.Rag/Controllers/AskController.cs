using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.Rag.Query;

namespace Phase4.Rag.Controllers;

// The query path: question → hybrid search (top 20) → rerank (top 5) → prompt → LLM → checked citations.
[ApiController]
[Route("ask")]
public class AskController(
    HybridSearch search,
    Reranker reranker,
    PromptBuilder prompts,
    IChatClient chat,
    CitationChecker citations) : ControllerBase
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

        // 1. Find candidates the user is allowed to see (permission filter is inside the SQL).
        var candidates = await search.SearchAsync(req.Question, tenantId, userGroups, topK: 20, ct);

        // 2. Rerank: keep the best 5, and only those that really answer the question.
        var ranked = (await reranker.RerankAsync(req.Question, candidates, topN: 5, ct))
                     .Where(r => r.Score >= Reranker.MinScore).ToList();
        if (ranked.Count == 0)
            return Ok(new { answer = "I don't know: none of the documents you can see cover this.", citations = Array.Empty<Citation>() });
            // no LLM call: nothing to answer from, so nothing to make up

        // 3. Build the prompt, 4. call the LLM (temperature 0: same sources → same answer).
        var prompt = prompts.Build(req.Question, ranked);
        var response = await chat.GetResponseAsync(prompt.Messages, new ChatOptions { Temperature = 0 }, ct);

        // 5. Keep only citations that point at a source we really sent.
        var (answer, cited) = citations.Check(response.Text, prompt.Sources);
        return Ok(new { answer, citations = cited });
    }
}
