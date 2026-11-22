using Microsoft.AspNetCore.Mvc;
using Phase4.Agents.Domain;
using Phase4.Agents.MultiAgent;

namespace Phase4.Agents.Controllers;

// The same support case as SupportController, handled by several agents:
// an orchestrator splits the message, specialist workers handle their part in parallel, the orchestrator combines.
[ApiController]
[Route("multi-agent")]
public class MultiAgentSupportController(Orchestrator orchestrator, OrderStore store) : ControllerBase
{
    public record ChatRequest(string ConversationId, string Message);

    // POST /multi-agent/chat   header X-User-Id: alice   body { conversationId, message }
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct)
    {
        // real app: userId from the JWT. Every worker's tools are built for THIS user.
        var workers = Workers.For(store, userId, req.ConversationId);
        var result = await orchestrator.RunAsync(req.Message, workers, ct);

        // Reports are returned here to SEE each worker; a real app logs them and returns only the answer.
        return Ok(new
        {
            answer = result.Answer,
            result.Tokens,
            workers = result.Reports.Select(r => new
            {
                worker = r.Worker.ToString(),
                r.Task,
                stopReason = r.Result.StopReason.ToString(),
                answer = r.Result.Answer,
                r.ApprovalId,
                r.Result.Steps,
            }),
        });
    }
}
