using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace M22.LlmApps.Controllers;

// Tool calling in one class. The model can't see our data, so we give it tools.
// The model only ASKS for a tool; this code runs it, checks who owns the order, and sends the result back.
// The model picks WHICH tool(s) from the question - we don't write an "if" per kind of question.
[ApiController]
[Route("api/chat")]
public class OrderChatController(IChatClient chat) : ControllerBase
{
    public record ChatRequest(string Question);
    private record Order(string Owner, string Item, DateOnly PlacedOn, string Status);

    private static readonly ConcurrentDictionary<string, Order> Orders = new()
    {
        ["123"] = new("alice", "running shoes", new(2026, 11, 10), "shipped, arrives Friday"),
        ["124"] = new("alice", "headphones",    new(2026, 11, 18), "processing"),
        ["456"] = new("bob",   "laptop",        new(2026, 11, 15), "processing"),
    };
    private static readonly Dictionary<string, int> ReturnDays = new() { ["shoes"] = 30, ["electronics"] = 15 };
    private static readonly ConcurrentDictionary<string, string> PendingCancels = new();   // orderId → userId

    private string _userId = "";

    // POST /api/chat   header X-User-Id: alice   body { "question": "Where's my last order?" }
    [HttpPost]
    public async Task<IActionResult> Ask(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId)
    {
        _userId = userId;   // real app: from the JWT (User.FindFirst("sub")), never from the prompt

        var options = new ChatOptions
        {
            Temperature = 0,
            Tools =   // each C# method → tool name + JSON schema; the model reads the [Description]s to choose
            [
                AIFunctionFactory.Create(GetOrderStatus),
                AIFunctionFactory.Create(GetRecentOrders),
                AIFunctionFactory.Create(GetReturnPolicy),
                AIFunctionFactory.Create(RequestCancelOrder),
            ],
        };
        ChatMessage[] messages =
        [
            new(ChatRole.System, "You help customers with their orders. Use the tools; never guess. " +
                                 "Today is 2026-11-21. To cancel, call RequestCancelOrder and tell the user to confirm. " +
                                 "If no tool can do what the user asks (e.g. change an address), say you can't and " +
                                 "point them to the Orders page. Never say an action was done unless a tool confirmed it."),
            new(ChatRole.User, req.Question),
        ];

        var response = await chat.GetResponseAsync(messages, options);   // may run several tools before answering
        return Ok(new { answer = response.Text });
    }

    // POST /api/chat/cancel/124/confirm  - the user clicks "Yes, cancel" in the UI. A human approves, not the model.
    [HttpPost("cancel/{orderId}/confirm")]
    public IActionResult ConfirmCancel(string orderId, [FromHeader(Name = "X-User-Id")] string userId)
    {
        if (!PendingCancels.TryRemove(new(orderId, userId))) return NotFound();
        Orders[orderId] = Orders[orderId] with { Status = "cancelled" };
        return Ok(new { orderId, status = "cancelled" });
    }

    // ----- tools (the model can call these; each one checks the owner itself) -----

    [Description("Get the status of one of the current customer's orders")]
    private string GetOrderStatus([Description("The order id, e.g. 123")] string orderId) =>
        Orders.TryGetValue(orderId, out var o) && o.Owner == _userId ? o.Status : "order not found";

    [Description("List the current customer's orders, newest first, with order id, item and date")]
    private string GetRecentOrders() =>
        string.Join("\n", Orders.Where(o => o.Value.Owner == _userId)
                                .OrderByDescending(o => o.Value.PlacedOn)
                                .Select(o => $"{o.Key}: {o.Value.Item}, placed {o.Value.PlacedOn}"));

    [Description("Get the return window in days for a product category, e.g. shoes or electronics")]
    private string GetReturnPolicy([Description("shoes or electronics")] string category) =>
        ReturnDays.TryGetValue(category.ToLowerInvariant(), out var days) ? $"{days} days" : "30 days (default)";

    [Description("Ask to cancel one of the current customer's orders. Does NOT cancel: the customer must confirm.")]
    private string RequestCancelOrder([Description("The order id")] string orderId)
    {
        if (!Orders.TryGetValue(orderId, out var o) || o.Owner != _userId) return "order not found";
        if (o.Status.StartsWith("shipped")) return "can't cancel: already shipped";
        PendingCancels[orderId] = _userId;                     // the model can only REQUEST; nothing changes yet
        return $"pending confirmation: POST /api/chat/cancel/{orderId}/confirm";
    }
}
