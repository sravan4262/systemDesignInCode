using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Phase4.Agents.Traces;

namespace Phase4.Agents.Support;

public enum StopReason { Answered, AwaitingApproval, MaxSteps, TokenBudget, Timeout, RepeatedCall }
public record AgentBudget(int MaxSteps = 8, long MaxTokens = 20_000, int TimeoutSeconds = 60);
public record AgentStep(int Step, string Tool, string Arguments, string Result);
public record AgentResult(string Answer, StopReason StopReason, List<AgentStep> Steps, long Tokens);

// The agent loop, written by hand (not UseFunctionInvocation) so OUR code decides when to stop:
//   ask the model → it answers (done) or asks for tools → we run them → send the results back → repeat.
// Stops on: an answer, an action waiting for a human (ApprovalGate), max steps, token budget, timeout,
// or the same tool call twice (stuck in a loop).
public class AgentLoop(IChatClient chat)
{
    private const string HandOff = "I couldn't finish this automatically. A support team member will follow up.";

    // trace (optional, step 5): when given, every model call and tool call is recorded in it.
    public async Task<AgentResult> RunAsync(List<ChatMessage> messages, IList<AITool> tools, AgentBudget budget, ApprovalGate gate, CancellationToken ct,
        AgentTrace? trace = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(budget.TimeoutSeconds));

        var options = new ChatOptions { Temperature = 0, Tools = tools };
        var steps = new List<AgentStep>();
        var seenCalls = new HashSet<string>();
        long tokens = 0;
        AgentResult Stop(StopReason why) => new(HandOff, why, steps, tokens);

        try
        {
            for (int step = 1; step <= budget.MaxSteps; step++)
            {
                var modelWatch = Stopwatch.StartNew();
                var response = await chat.GetResponseAsync(messages, options, timeout.Token);
                tokens += response.Usage?.TotalTokenCount ?? 0;
                messages.AddRange(response.Messages);   // keep the model's tool requests in the history

                var calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
                trace?.AddModelCall(step, response.Usage, modelWatch.ElapsedMilliseconds, calls.Select(c => c.Name));
                if (calls.Count == 0) return new(response.Text, StopReason.Answered, steps, tokens);   // done
                if (tokens > budget.MaxTokens) return Stop(StopReason.TokenBudget);

                foreach (var call in calls)
                {
                    var args = JsonSerializer.Serialize(call.Arguments);
                    if (!seenCalls.Add($"{call.Name}:{args}")) return Stop(StopReason.RepeatedCall);   // same call again = stuck

                    var toolWatch = Stopwatch.StartNew();
                    var result = gate.IsWaiting
                        ? "skipped: an action is waiting for human approval; no more actions in this task"   // Fix 2 (every call still needs a result)
                        : await RunToolAsync(tools, call, timeout.Token);
                    steps.Add(new(step, call.Name, args, result));
                    trace?.AddToolCall(step, call.Name, args, result, toolWatch.ElapsedMilliseconds);
                    messages.Add(new(ChatRole.Tool, [new FunctionResultContent(call.CallId, result)]));
                }

                if (gate.IsWaiting)   // Fix 2: one last call with NO tools, so the model can only write text
                {
                    var finalWatch = Stopwatch.StartNew();
                    var final = await chat.GetResponseAsync(messages, new ChatOptions { Temperature = 0 }, timeout.Token);
                    tokens += final.Usage?.TotalTokenCount ?? 0;
                    trace?.AddModelCall(step, final.Usage, finalWatch.ElapsedMilliseconds, []);
                    return new(final.Text, StopReason.AwaitingApproval, steps, tokens);
                }
            }
            return Stop(StopReason.MaxSteps);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Stop(StopReason.Timeout);   // our timeout fired, not the caller hanging up
        }
    }

    // A tool error goes BACK to the model as text ("error: ..."), so it can correct itself instead of the request failing.
    private static async Task<string> RunToolAsync(IList<AITool> tools, FunctionCallContent call, CancellationToken ct)
    {
        if (tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == call.Name) is not { } tool)
            return $"error: there is no tool named {call.Name}";
        try
        {
            return (await tool.InvokeAsync(new AIFunctionArguments(call.Arguments), ct))?.ToString() ?? "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"error: {ex.Message}";   // e.g. "reason" wasn't one of the enum values
        }
    }
}
