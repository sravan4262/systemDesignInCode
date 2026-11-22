using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.Agents.Domain;
using Phase4.Agents.Memory;
using Phase4.Agents.Support;

namespace Phase4.Agents.Controllers;

// The support agent with memory: it remembers this chat (short-term) and the customer's stated
// preferences across chats (long-term). ContextBuilder decides what of that the model sees.
[ApiController]
[Route("memory-agent")]
public class MemorySupportController(
    AgentLoop agent,
    OrderStore orders,
    MemoryStore memoryStore,
    ConversationStore conversations,
    ContextBuilder context) : ControllerBase
{
    public record ChatRequest(string ConversationId, string Message);

    private const string SystemPrompt = """
        You are a support agent for an online shop. Use the tools; never guess order details.
        Refund only delivered orders, for a reason the customer gave. If a refund waits for approval, say so.
        If the customer states a preference about themselves (how to contact them, language, delivery notes),
        save it with RememberPreference. Respect saved preferences.
        """;

    // POST /memory-agent/chat   header X-User-Id: alice   body { conversationId, message }   (send several turns, same id)
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct)
    {
        // 1. Long-term memory for THIS user, 2. short-term memory for THIS chat → 3. the context for this turn.
        var memories = await memoryStore.GetAsync(userId, ct);
        var messages = await context.BuildAsync(SystemPrompt, req.ConversationId, userId, memories, req.Message, ct);
        var contextMessages = messages.Count;   // before the loop adds tool calls

        // 4. The same agent loop, with one extra tool: RememberPreference.
        var gate = new ApprovalGate();
        IList<AITool> tools = [.. new SupportTools(orders, userId, req.ConversationId, gate).All, .. new MemoryTools(memoryStore, userId).All];
        var result = await agent.RunAsync(messages, tools, new AgentBudget(), gate, ct);

        // 5. Save this turn (text only: the tool calls of this turn aren't needed next turn).
        await conversations.AppendTurnAsync(req.ConversationId, userId, req.Message, result.Answer, ct);

        return Ok(new
        {
            answer = result.Answer,
            stopReason = result.StopReason.ToString(),
            memories,               // what long-term memory held at the start of this turn
            contextMessages,        // how many messages the model got: stays small however long the chat gets
            result.Tokens,
            result.Steps,
        });
    }
}
