using System.ComponentModel;
using Microsoft.Extensions.AI;
using Phase4.Agents.Support;

namespace Phase4.Agents.MultiAgent;

public record Subtask(
    WorkerKind Worker,
    [property: Description("The part of the customer's message this worker handles, as a clear instruction with ids")] string Task);
public record Plan(List<Subtask> Subtasks);
public record WorkerReport(WorkerKind Worker, string Task, AgentResult Result, string? ApprovalId);
public record MultiAgentResult(string Answer, List<WorkerReport> Reports, long Tokens);

// Orchestrator–worker:
//   1. plan    (LLM, structured output): split the message into subtasks, one per worker
//   2. workers (AgentLoop each, IN PARALLEL): small context = only their task + only their tools
//   3. combine (LLM, no tools): the workers' short reports → one reply
// Why: each worker sees a short prompt with 1–2 tools instead of one agent juggling everything, and the
// subtasks run at the same time. Cost: more LLM calls in total (plan + each worker + combine).
public class Orchestrator(IChatClient chat, AgentLoop agent)
{
    private static readonly ChatOptions NoTools = new() { Temperature = 0 };
    private static readonly AgentBudget WorkerBudget = new(MaxSteps: 4, MaxTokens: 6_000, TimeoutSeconds: 30);   // smaller job, smaller budget

    private const string PlannerPrompt = """
        Split the customer's message into subtasks for these workers. Include only the workers needed.
        - Orders: status and details of the customer's orders (read only)
        - Refunds: refund requests for delivered orders
        - Policy: return windows and rules
        """;

    private const string CombinePrompt = """
        Write one short, friendly reply to the customer using ONLY the worker reports below.
        Don't add facts that aren't in a report. If a report says a refund is waiting for approval, say exactly that.
        If a report says a worker couldn't finish, tell the customer a team member will follow up on that part.
        """;

    public async Task<MultiAgentResult> RunAsync(string message, Dictionary<WorkerKind, Worker> workers, CancellationToken ct)
    {
        // 1. Plan
        var planned = await chat.GetResponseAsync<Plan>(
            [new(ChatRole.System, PlannerPrompt), new(ChatRole.User, message)], NoTools, cancellationToken: ct);
        long tokens = planned.Usage?.TotalTokenCount ?? 0;

        // Code checks the plan: at most one subtask per worker (two parallel Refunds workers could race on one order).
        var subtasks = planned.TryGetResult(out var plan) ? plan.Subtasks.DistinctBy(s => s.Worker).ToList() : [];
        if (subtasks.Count == 0)
            return new("I couldn't work out what you need. A support team member will follow up.", [], tokens);

        // 2. Workers, in parallel. Each gets a FRESH message list: its own instructions + its task, nothing else.
        var reports = await Task.WhenAll(subtasks.Select(s => RunWorkerAsync(workers[s.Worker], s, ct)));
        tokens += reports.Sum(r => r.Result.Tokens);

        // 3. Combine: the orchestrator sees only the short reports, not the workers' tool calls and raw results.
        var reportText = string.Join("\n", reports.Select(r =>
            $"- {r.Worker} ({r.Result.StopReason}): {r.Result.Answer}"));
        var final = await chat.GetResponseAsync(
        [
            new(ChatRole.System, CombinePrompt),
            new(ChatRole.User, $"Customer message:\n{message}\n\nWorker reports:\n{reportText}"),
        ], NoTools, ct);
        tokens += final.Usage?.TotalTokenCount ?? 0;

        return new(final.Text, [.. reports], tokens);
    }

    private async Task<WorkerReport> RunWorkerAsync(Worker worker, Subtask subtask, CancellationToken ct)
    {
        var gate = new ApprovalGate();
        List<ChatMessage> messages = [new(ChatRole.System, worker.Instructions), new(ChatRole.User, subtask.Task)];
        var result = await agent.RunAsync(messages, worker.Tools(gate), WorkerBudget, gate, ct);
        return new(subtask.Worker, subtask.Task, result, gate.PendingApprovalId);
    }
}
