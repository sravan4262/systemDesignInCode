using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Phase4.ProductionAi.Guardrails;

namespace Phase4.ProductionAi.Controllers;

// Guardrails around a plain LLM call, in two versions picked by the URL:
//   /guardrails/code/...     → RegexPiiRedactor + KeywordModerator          (in code)
//   /guardrails/service/...  → AzurePiiRedactor + AzureContentSafetyModerator (by calling an endpoint)
// Same pipeline, same thresholds, same responses: compare what each version catches.
[ApiController]
[Route("guardrails/{mode}")]
public class GuardrailsController(IServiceProvider services, IChatClient chat) : ControllerBase
{
    public record ChatRequest(string Message);

    private const string SystemPrompt = "You are a support assistant for an online shop. Answer briefly. Never repeat personal data.";
    private const string Refusal = "Sorry, I can't help with that. If you need support with an order, I'm happy to help.";
    private const string HeldForReview = "Thanks, a member of our team will look at this and get back to you.";

    // POST /guardrails/code/check   body { message }   → input guards only, no LLM (try both modes without an OpenAI key)
    [HttpPost("check")]
    public async Task<IActionResult> Check(string mode, ChatRequest req, CancellationToken ct)
    {
        if (Pipeline(mode) is not { } pipeline) return NotFound($"mode must be 'code' or 'service', not '{mode}'");
        var input = await pipeline.CheckInputAsync(req.Message, ct);
        var sent = input.Action is GuardAction.Allow or GuardAction.Redact;   // Block/Review never reach the model
        return Ok(new { action = input.Action, modelWouldSee = sent ? input.Text : null, decisions = input.Results });
    }

    // POST /guardrails/code/chat   body { message }   → input guards → LLM → output guards
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(string mode, ChatRequest req, CancellationToken ct)
    {
        if (Pipeline(mode) is not { } pipeline) return NotFound($"mode must be 'code' or 'service', not '{mode}'");

        // 1. Input guards. The model is never called for blocked or held input (no cost, no risk).
        var input = await pipeline.CheckInputAsync(req.Message, ct);
        if (input.Action is GuardAction.Block or GuardAction.Review)
            return Ok(Reply(input.Action == GuardAction.Block ? Refusal : HeldForReview, input, output: null));
            // real app: Review → a row in a review queue with the conversation id, for a human to answer

        // 2. The LLM sees the REDACTED text only.
        var response = await chat.GetResponseAsync(
            [new(ChatRole.System, SystemPrompt), new(ChatRole.User, input.Text)], new ChatOptions { Temperature = 0 }, ct);

        // 3. Output guards: the user sees a checked reply, or a safe fallback.
        var output = await pipeline.CheckOutputAsync(response.Text, ct);
        var answer = output.Action switch
        {
            GuardAction.Block => Refusal,
            GuardAction.Review => HeldForReview,   // real app: hold the reply until a human approves it
            _ => output.Text,                      // Allow, or Redact (PII masked in the reply)
        };
        return Ok(Reply(answer, input, output));
    }

    // Keyed services: "code" and "service" are two registrations of the same type (see Program.cs).
    private GuardrailPipeline? Pipeline(string mode) => services.GetKeyedService<GuardrailPipeline>(mode.ToLowerInvariant());

    // Decisions are returned to SEE the guards work; a real app logs them in the trace and returns only the answer.
    private static object Reply(string answer, GuardOutcome input, GuardOutcome? output) => new
    {
        answer,
        modelSaw = input.Action is GuardAction.Allow or GuardAction.Redact ? input.Text : null,
        input = input.Results,
        output = output?.Results,
    };
}
