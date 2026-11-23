using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.Agents.Domain;
using Phase4.Agents.Support;
using Phase4.Agents.Traces;

namespace Phase4.Agents.Controllers;

// Step 5: traces. The same support agent as SupportController, but every run is recorded and saved:
// each model call (tokens, ms, cost, tools it asked for) and each tool call (args, result, ms).
// The customer gets only the answer + a traceId; support/engineers open the trace to see WHY.
[ApiController]
[Route("traces")]
public class TracesController(AgentLoop agent, OrderStore store, AgentTraceStore traces, IConfiguration config,
    ILogger<TracesController> logger) : ControllerBase
{
    public record ChatRequest(string ConversationId, string Message);

    // POST /traces/support/chat   header X-User-Id: alice   body { conversationId, message }
    [HttpPost("support/chat")]
    public async Task<IActionResult> Chat(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct)
    {
        var trace = new AgentTrace
        {
            UserId = userId,
            Message = req.Message,
            Model = config["OpenAI:Model"] ?? "gpt-4.1-mini",
            PromptVersion = SupportController.PromptVersion,
        };

        var gate = new ApprovalGate();
        var tools = new SupportTools(store, userId, req.ConversationId, gate).All;
        List<ChatMessage> messages = [new(ChatRole.System, SupportController.SystemPrompt), new(ChatRole.User, req.Message)];

        var watch = Stopwatch.StartNew();
        var result = await agent.RunAsync(messages, tools, new AgentBudget(), gate, ct, trace);   // the loop fills the trace
        (trace.StopReason, trace.Answer, trace.TotalMs) = (result.StopReason.ToString(), result.Answer, watch.ElapsedMilliseconds);

        // Saving the trace must never fail the customer's request: log and carry on.
        try { await traces.SaveAsync(trace, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not save trace {TraceId}", trace.TraceId); }

        return Ok(new
        {
            answer = result.Answer,
            stopReason = trace.StopReason,
            approvalId = gate.PendingApprovalId,
            traceId = trace.TraceId,
            trace.TotalTokens,
            trace.CostUsd,
        });
    }

    // GET /traces/summary?days=7   header X-User-Id: alice   → runs, resolved, total cost, cost per resolved task
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct, int days = 7) =>
        Ok(await traces.SummaryAsync(userId, days, ct));

    // GET /traces/{traceId}   header X-User-Id: alice   → the full trace (real app: support role only, from the JWT)
    [HttpGet("{traceId}")]
    public async Task<IActionResult> Get(string traceId, [FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct) =>
        await traces.GetAsync(userId, traceId, ct) is { } json ? Content(json, "application/json") : NotFound();
}
