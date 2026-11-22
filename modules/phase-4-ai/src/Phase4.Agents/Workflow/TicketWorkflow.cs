using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Phase4.Agents.Workflow;

public enum TicketCategory { Refund, Delivery, Account, Technical, Other }
public enum Urgency { Low, High }

public record Classification(
    TicketCategory Category,
    Urgency Urgency,
    [property: Description("One short sentence: why this category")] string Reason);

public record TicketResult(string Summary, Classification Classification, string Queue);

// A WORKFLOW: the steps are fixed in code, always the same 3, always in this order.
// The model fills in each step (write a summary, pick a category); it never decides what happens next.
//
//   ticket ─► 1. summarise (LLM) ─► 2. classify (LLM, structured output) ─► 3. route (plain code) ─► queue
//
// Agent (SupportController) vs. workflow (this):
//   agent    = the model picks the next step from the results so far; good when steps vary per case
//   workflow = code picks the steps; cheaper, faster, predictable, easy to test; good when steps are known
public class TicketWorkflow(IChatClient chat)
{
    private static readonly ChatOptions Fixed = new() { Temperature = 0 };   // no tools: the model can't act, only write

    public async Task<TicketResult> RunAsync(string subject, string body, CancellationToken ct)
    {
        var summary = await SummariseAsync(subject, body, ct);
        var classification = await ClassifyAsync(summary, ct);
        var queue = Route(classification);
        return new(summary, classification, queue);
    }

    // Step 1 (LLM): long ticket → 1–2 sentences, so step 2 and the human reading the queue see only what matters.
    private async Task<string> SummariseAsync(string subject, string body, CancellationToken ct)
    {
        var response = await chat.GetResponseAsync(
        [
            new(ChatRole.System, "Summarise this support ticket in at most 2 sentences: what the customer wants and why."),
            new(ChatRole.User, $"Subject: {subject}\n\n{body}"),
        ], Fixed, ct);
        return response.Text;
    }

    // Step 2 (LLM): structured output, so we get enum values back, not free text to parse.
    private async Task<Classification> ClassifyAsync(string summary, CancellationToken ct)
    {
        var response = await chat.GetResponseAsync<Classification>(
        [
            new(ChatRole.System, "Classify the support ticket. High urgency only if the customer can't use the product or money is at risk."),
            new(ChatRole.User, summary),
        ], Fixed, cancellationToken: ct);

        // The model's JSON didn't fit the schema (rare)? Don't guess: send it to a human.
        return response.TryGetResult(out var result) ? result : new(TicketCategory.Other, Urgency.High, "could not classify");
    }

    // Step 3 (plain code, no LLM): routing is a business rule, so it lives in code where it's exact and testable.
    private static string Route(Classification c) => (c.Category, c.Urgency) switch
    {
        (TicketCategory.Other, _)            => "human-triage",       // unsure → a person decides
        (TicketCategory.Refund, _)           => "support-agent",      // the agent (SupportController) can handle it
        (TicketCategory.Technical, Urgency.High) => "engineering-oncall",
        (_, Urgency.High)                    => $"{c.Category.ToString().ToLowerInvariant()}-priority",
        _                                    => c.Category.ToString().ToLowerInvariant(),
    };
}
