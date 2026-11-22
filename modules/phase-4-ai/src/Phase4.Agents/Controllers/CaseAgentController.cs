using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.Agents.Domain;
using Phase4.Agents.MultiAgent;

namespace Phase4.Agents.Controllers;

// Multi-agent + shared state: the same orchestrator and workers as MultiAgentSupportController,
// but every worker also records what it did on ONE shared support case, at the same time.
[ApiController]
[Route("case-agent")]
public class CaseAgentController(Orchestrator orchestrator, OrderStore orders, CaseStore cases) : ControllerBase
{
    public record ChatRequest(string CaseId, string Message);

    // POST /case-agent/chat   header X-User-Id: alice   body { caseId, message }
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct)
    {
        await cases.EnsureAsync(req.CaseId, userId, ct);

        // The usual workers, each with the case tools added (named per worker, so notes say who wrote them).
        var workers = Workers.For(orders, userId, req.CaseId).ToDictionary(w => w.Key, w => w.Value with
        {
            Instructions = w.Value.Instructions + " When you're done, record what you did with AddCaseNote.",
            Tools = gate => [.. w.Value.Tools(gate), .. new CaseTools(cases, req.CaseId, userId, w.Key.ToString()).All],
        });

        var result = await orchestrator.RunAsync(req.Message, workers, ct);
        var finalCase = await cases.GetAsync(req.CaseId, userId, ct);

        return Ok(new { answer = result.Answer, @case = finalCase, result.Tokens });
    }
}
