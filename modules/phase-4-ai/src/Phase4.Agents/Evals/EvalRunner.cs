using System.Text.Json;
using Microsoft.Extensions.AI;
using Phase4.Agents.Controllers;
using Phase4.Agents.Support;

namespace Phase4.Agents.Evals;

public record TrialResult(int Trial, bool Passed, List<string> Failures, string StopReason, string Answer, long Tokens);
public record CaseResult(string Id, bool Critical, int PassedRuns, int Runs, bool Passed, List<TrialResult> Trials);
public record EvalReport(bool GatePassed, string PromptVersion, double PassRate, long Tokens, List<CaseResult> Cases);

// Runs every golden case N times against the REAL agent loop, prompt and tools (fake data only).
// Gate: critical cases pass every trial; other cases pass at least 2 of 3. Anything less blocks the change.
// Run it on every prompt / model / tool change, like unit tests, and add each production failure as a new case.
public class EvalRunner(AgentLoop agent)
{
    public async Task<EvalReport> RunAsync(int trials, CancellationToken ct)
    {
        var cases = new List<CaseResult>();
        foreach (var c in GoldenCases.All)
        {
            var runs = await Task.WhenAll(Enumerable.Range(1, trials).Select(t => RunTrialAsync(c, t, ct)));   // trials in parallel
            int passed = runs.Count(r => r.Passed);
            bool casePassed = c.Critical ? passed == trials : passed * 3 >= trials * 2;
            cases.Add(new(c.Id, c.Critical, passed, trials, casePassed, [.. runs]));
        }

        var all = cases.SelectMany(c => c.Trials).ToList();
        return new(
            GatePassed: cases.All(c => c.Passed),
            PromptVersion: SupportController.PromptVersion,
            PassRate: Math.Round((double)all.Count(t => t.Passed) / all.Count, 2),
            Tokens: all.Sum(t => t.Tokens),
            Cases: cases);
    }

    private async Task<TrialResult> RunTrialAsync(GoldenCase c, int trial, CancellationToken ct)
    {
        var store = new InMemoryOrderStore();                                    // fresh data every trial
        var gate = new ApprovalGate();
        var tools = new SupportTools(store, c.UserId, $"eval-{c.Id}-{trial}", gate).All;
        List<ChatMessage> messages = [new(ChatRole.System, SupportController.SystemPrompt), new(ChatRole.User, c.Message)];

        var result = await agent.RunAsync(messages, tools, new AgentBudget(), gate, ct);
        var failures = Check(c, result, store);
        return new(trial, failures.Count == 0, failures, result.StopReason.ToString(), result.Answer, result.Tokens);
    }

    // Every check that fails adds a reason, so a red result says WHAT went wrong, not just "failed".
    private static List<string> Check(GoldenCase c, AgentResult r, InMemoryOrderStore store)
    {
        var failures = new List<string>();
        var answer = r.Answer.ToLowerInvariant();

        if (r.StopReason != c.ExpectedStop) failures.Add($"stop: expected {c.ExpectedStop}, got {r.StopReason}");

        foreach (var e in c.MustCall ?? [])
            if (!r.Steps.Any(s => s.Tool == e.Tool && ArgsMatch(s.Arguments, e.Args)))
                failures.Add($"tool: never called {e.Tool}({string.Join(", ", e.Args ?? [])})");

        foreach (var tool in c.MustNotCall ?? [])
            if (r.Steps.Any(s => s.Tool == tool)) failures.Add($"tool: called {tool}, which it must not");

        if (c.AnswerMustContainAny is { Length: > 0 } any && !any.Any(w => answer.Contains(w.ToLowerInvariant())))
            failures.Add($"answer: contains none of [{string.Join(", ", any)}]");

        foreach (var bad in c.AnswerMustNotContain ?? [])
            if (answer.Contains(bad.ToLowerInvariant())) failures.Add($"answer: contains \"{bad}\"");

        if (c.StateCheck?.Invoke(store) is { } stateFailure) failures.Add($"state: {stateFailure}");
        return failures;
    }

    // Arguments are the JSON the model sent, e.g. {"orderId":"124","amount":40,"reason":"Damaged"}.
    // Compare by value: numbers as decimals (40 == 40.0), text case-insensitively.
    private static bool ArgsMatch(string argumentsJson, Dictionary<string, string>? expected)
    {
        if (expected is null) return true;
        using var doc = JsonDocument.Parse(argumentsJson);
        foreach (var (name, want) in expected)
        {
            var prop = doc.RootElement.EnumerateObject().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (prop.Value.ValueKind == JsonValueKind.Undefined) return false;

            var got = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText();
            bool same = decimal.TryParse(got, out var g) && decimal.TryParse(want, out var w)
                ? g == w
                : string.Equals(got, want, StringComparison.OrdinalIgnoreCase);
            if (!same) return false;
        }
        return true;
    }
}
