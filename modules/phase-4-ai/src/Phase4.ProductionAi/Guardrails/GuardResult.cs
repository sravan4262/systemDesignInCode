namespace Phase4.ProductionAi.Guardrails;

// What a guard decided. Most traffic is Allow; Redact lets the request continue with masked text;
// Review = the grey zone, a human decides; Block = stop with a safe reply.
public enum GuardAction { Allow, Redact, Review, Block }

// One line per guard that ran, so the response/trace shows WHY something was blocked or held.
public record GuardResult(string Guard, GuardAction Action, string? Reason = null, double? Score = null);

// The outcome of running all guards on one text: the (possibly redacted) text + every decision.
// The worst action wins: Block > Review > Redact > Allow.
public record GuardOutcome(string Text, List<GuardResult> Results)
{
    public GuardAction Action => Results.Count == 0 ? GuardAction.Allow : Results.Max(r => r.Action);
}

// Two numbers per check, not one: below ReviewAt allow, between the two a human looks, at/above BlockAt block.
//   0 ──── ReviewAt (0.5) ──── BlockAt (0.8) ──── 1
//    allow      human review         block
public record GuardThresholds(double ReviewAt = 0.5, double BlockAt = 0.8, int MaxInputChars = 4000);
