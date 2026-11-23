namespace Phase4.ProductionAi.Guardrails;

// Runs the guards in a fixed order, cheapest first, and returns every decision.
//   input:  length → PII redaction → moderation        (the model only ever sees the redacted text)
//   output: PII leak → moderation                        (the user only ever sees a checked reply)
// The same pipeline class runs both versions; only the redactor/moderator behind it differ (see Program.cs).
//
// When a guard SERVICE fails, the answer is decided per guard:
//   PII  → fail CLOSED: block. Sending unredacted card numbers to the model and logs is the worse outcome.
//   moderation → fall back to the in-code moderator (if given) instead of failing open with no check at all.
public class GuardrailPipeline(IPiiRedactor redactor, IModerator moderator, GuardThresholds thresholds,
    ILogger<GuardrailPipeline> logger, IModerator? fallbackModerator = null)
{
    public async Task<GuardOutcome> CheckInputAsync(string text, CancellationToken ct)
    {
        var results = new List<GuardResult>();

        if (text.Length > thresholds.MaxInputChars)   // cheapest check first: no call, no cost
            return new(text, [new("length", GuardAction.Block, $"over {thresholds.MaxInputChars} characters")]);

        (text, var pii) = await RedactAsync(text, "input.pii", ct);
        results.Add(pii);
        if (pii.Action == GuardAction.Block) return new(text, results);

        results.Add(await ModerateAsync(text, "input.moderation", ct));
        return new(text, results);
    }

    public async Task<GuardOutcome> CheckOutputAsync(string reply, CancellationToken ct)
    {
        // A reply can contain PII the user never typed (from a retrieved document or a tool result).
        (reply, var pii) = await RedactAsync(reply, "output.pii", ct);
        return new(reply, [pii, await ModerateAsync(reply, "output.moderation", ct)]);
    }

    private async Task<(string Text, GuardResult Result)> RedactAsync(string text, string guard, CancellationToken ct)
    {
        try
        {
            var r = await redactor.RedactAsync(text, ct);
            return r.Found.Count == 0
                ? (text, new(guard, GuardAction.Allow))
                : (r.RedactedText, new(guard, GuardAction.Redact, $"masked: {string.Join(", ", r.Found)}"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "PII service failed: failing closed");
            return (text, new(guard, GuardAction.Block, "PII check unavailable, so the text can't be sent safely"));
        }
    }

    private async Task<GuardResult> ModerateAsync(string text, string guard, CancellationToken ct)
    {
        Dictionary<string, double> scores;
        var source = "";
        try
        {
            scores = await moderator.ScoreAsync(text, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && fallbackModerator is not null)
        {
            logger.LogWarning(ex, "Moderation service failed: using the in-code fallback");
            scores = await fallbackModerator.ScoreAsync(text, ct);
            source = " (fallback: in-code)";
        }

        if (scores.Count == 0 || scores.Values.Max() < thresholds.ReviewAt) return new(guard + source, GuardAction.Allow);

        var (category, score) = scores.MaxBy(s => s.Value);
        var action = score >= thresholds.BlockAt ? GuardAction.Block : GuardAction.Review;
        return new(guard + source, action, category, Math.Round(score, 2));
    }
}
