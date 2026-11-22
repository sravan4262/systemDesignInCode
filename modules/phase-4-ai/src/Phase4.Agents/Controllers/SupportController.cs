using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.Agents.Domain;
using Phase4.Agents.Support;

namespace Phase4.Agents.Controllers;

// The support agent: the model decides WHICH steps to take (look up, refund, ask), our code decides what's ALLOWED.
[ApiController]
[Route("support")]
public class SupportController(AgentLoop agent, OrderStore store) : ControllerBase
{
    public record ChatRequest(string ConversationId, string Message);

    private const string SystemPrompt = """
        You are a support agent for an online shop. Use the tools; never guess order details.
        Refund only delivered orders, and only for a reason the customer gave (damaged, not delivered, wrong item).
        If a tool says a refund is waiting for approval, tell the customer that; never say it was refunded.
        If you can't help, say so and offer a human.
        """;

    // POST /support/chat   header X-User-Id: alice   body { conversationId, message }
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct)
    {
        // real app: userId from the JWT. The tools are built for THIS user, so the model can't act for anyone else.
        var gate = new ApprovalGate();   // Fix 2: shared by the tools (mark) and the loop (stop)
        var tools = new SupportTools(store, userId, req.ConversationId, gate).All;
        List<ChatMessage> messages = [new(ChatRole.System, SystemPrompt), new(ChatRole.User, req.Message)];

        var result = await agent.RunAsync(messages, tools, new AgentBudget(), gate, ct);

        // Steps are returned here to SEE the agent work; a real app logs them (step 5: traces) and returns only the answer.
        return Ok(new { answer = result.Answer, stopReason = result.StopReason.ToString(), approvalId = gate.PendingApprovalId, result.Tokens, result.Steps });
    }

    // POST /support/approvals/apr-1a2b/approve   header X-User-Role: support   tier 2: a human approves big refunds
    [HttpPost("approvals/{approvalId}/approve")]
    public async Task<IActionResult> Approve(string approvalId, [FromHeader(Name = "X-User-Role")] string role, CancellationToken ct)
    {
        if (role != "support") return StatusCode(403);   // real app: [Authorize(Roles = "support")]
        return await store.ApproveAsync(approvalId, ct) is { } result ? Ok(new { approvalId, result }) : NotFound();
    }
}
