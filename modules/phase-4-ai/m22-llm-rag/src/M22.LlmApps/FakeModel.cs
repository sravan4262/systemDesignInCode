using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace M22.LlmApps;

// A scripted stand-in for a real model (no API key needed). It behaves like one would:
// no tools → guesses; with tools → asks for GetOrderStatus; after the tool result → writes the answer.
public class FakeModel : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        var list = messages.ToList();
        var result = list.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault();
        var orderId = Regex.Match(list[0].Text, @"\d+").Value;

        ChatMessage reply =
            options?.Tools is not { Count: > 0 } ? new(ChatRole.Assistant, $"Your order {orderId} shipped yesterday.")
            : result is null ? new(ChatRole.Assistant, [new FunctionCallContent("call-1", "GetOrderStatus",
                                    new Dictionary<string, object?> { ["orderId"] = orderId })])
            : new(ChatRole.Assistant, $"Order {orderId}: {result.Result}.");
        return Task.FromResult(new ChatResponse(reply));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var u in (await GetResponseAsync(messages, options, ct)).ToChatResponseUpdates()) yield return u;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
