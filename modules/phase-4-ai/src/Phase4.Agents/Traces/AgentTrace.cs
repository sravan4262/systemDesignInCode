using Microsoft.Extensions.AI;

namespace Phase4.Agents.Traces;

// One record per agent run: every model call (tokens, time, cost, which tools it asked for) and every tool call
// (arguments, result, time). "The agent did something wrong / cost too much" → open the trace:
//   wrong tool or wrong arguments   → prompt / tool descriptions
//   right tool, "error: ..." result → tool code or data
//   many model calls, high cost     → the loop isn't converging: stop conditions, context size
//   one slow step                   → that model call or tool (ModelCall.Ms / ToolCall.Ms)
public class AgentTrace
{
    public string TraceId { get; } = Guid.NewGuid().ToString("N");
    public required string UserId { get; init; }
    public required string Message { get; init; }
    public required string Model { get; init; }
    public required string PromptVersion { get; init; }   // "it got worse after the prompt change" → compare versions

    public record ModelCall(int Step, long InputTokens, long CachedInputTokens, long OutputTokens, long Ms, decimal CostUsd, List<string> ToolsRequested);
    public record ToolCall(int Step, string Tool, string Arguments, string Result, long Ms);

    public List<ModelCall> ModelCalls { get; } = [];
    public List<ToolCall> ToolCalls { get; } = [];

    public string StopReason { get; set; } = "";
    public string Answer { get; set; } = "";
    public long TotalMs { get; set; }
    public long TotalTokens => ModelCalls.Sum(c => c.InputTokens + c.OutputTokens);
    public decimal CostUsd => ModelCalls.Sum(c => c.CostUsd);

    // gpt-4.1-mini, USD per 1M tokens (same prices as RagTrace). Each step re-sends the whole history,
    // so input tokens grow every step: that's why long agent runs get expensive.
    private const decimal InputPerM = 0.40m, CachedPerM = 0.10m, OutputPerM = 1.60m;

    public void AddModelCall(int step, UsageDetails? usage, long ms, IEnumerable<string> toolsRequested)
    {
        long input = usage?.InputTokenCount ?? 0, cached = usage?.CachedInputTokenCount ?? 0, output = usage?.OutputTokenCount ?? 0;
        var cost = ((input - cached) * InputPerM + cached * CachedPerM + output * OutputPerM) / 1_000_000m;
        ModelCalls.Add(new(step, input, cached, output, ms, cost, [.. toolsRequested]));
    }

    public void AddToolCall(int step, string tool, string arguments, string result, long ms) =>
        ToolCalls.Add(new(step, tool, arguments, result, ms));
}
