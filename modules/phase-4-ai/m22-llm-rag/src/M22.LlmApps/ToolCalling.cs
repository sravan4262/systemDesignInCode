using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace M22.LlmApps;

// ❌ BEFORE: no tools. The model only knows its training data, so it GUESSES the order status.
public static class NoTools
{
    public static async Task<string> AskAsync(IChatClient model, string question) =>
        (await model.GetResponseAsync(question)).Text;          // → "Your order 123 shipped yesterday." (made up)
}

// ✅ AFTER: the model asks for GetOrderStatus; OUR code runs it, checks permission, and sends the result back.
//    UseFunctionInvocation runs the loop (tool call → result → model) and caps it at 5 rounds.
public class OrderTools(string currentUserId)
{
    private readonly Dictionary<string, (string Owner, string Status)> _orders = new()
    {
        ["123"] = ("alice", "shipped, arrives Friday"),
        ["456"] = ("bob",   "processing"),
    };

    [Description("Get the status of one of the current customer's orders")]
    public string GetOrderStatus([Description("The order id, e.g. 123")] string orderId) =>
        _orders.TryGetValue(orderId, out var o) && o.Owner == currentUserId   // permission check HERE, never in the prompt
            ? o.Status
            : "order not found";                                              // don't reveal someone else's order exists

    public static async Task<string> AskAsync(IChatClient model, string userId, string question)
    {
        var chat = new ChatClientBuilder(model)
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 5)   // stops a tool-call loop
            .Build();
        var tools = new OrderTools(userId);
        var options = new ChatOptions { Temperature = 0, Tools = [AIFunctionFactory.Create(tools.GetOrderStatus)] };
        return (await chat.GetResponseAsync(question, options)).Text;
    }
}
