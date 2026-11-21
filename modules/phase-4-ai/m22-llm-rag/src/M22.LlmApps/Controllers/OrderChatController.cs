using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace M22.LlmApps.Controllers;

// Tool calling in one class. The model can't see our data, so we give it a tool (GetOrderStatus).
// The model only ASKS for the tool; this code runs it, checks who owns the order, and sends the result back.
[ApiController]
[Route("api/chat")]
public class OrderChatController(IChatClient chat) : ControllerBase
{
    public record ChatRequest(string Question);

    private static readonly Dictionary<string, (string Owner, string Status)> Orders = new()
    {
        ["123"] = ("alice", "shipped, arrives Friday"),
        ["456"] = ("bob",   "processing"),
    };

    private string _userId = "";

    // POST /api/chat   header X-User-Id: alice   body { "question": "Where's my order 123?" }
    [HttpPost]
    public async Task<IActionResult> Ask(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId)
    {
        _userId = userId;   // real app: from the JWT (User.FindFirst("sub")), never from the prompt

        var options = new ChatOptions
        {
            Temperature = 0,
            Tools = [AIFunctionFactory.Create(GetOrderStatus)],   // C# method → tool name + JSON schema for the model
        };
        ChatMessage[] messages =
        [
            new(ChatRole.System, "You help customers with their orders. Use the tools; never guess an order status."),
            new(ChatRole.User, req.Question),
        ];

        var response = await chat.GetResponseAsync(messages, options);   // model → tool call → our method → model → answer
        return Ok(new { answer = response.Text });
    }

    [Description("Get the status of one of the current customer's orders")]
    private string GetOrderStatus([Description("The order id, e.g. 123")] string orderId) =>
        Orders.TryGetValue(orderId, out var o) && o.Owner == _userId   // permission check HERE, in code
            ? o.Status
            : "order not found";                                       // don't reveal that someone else's order exists
}
