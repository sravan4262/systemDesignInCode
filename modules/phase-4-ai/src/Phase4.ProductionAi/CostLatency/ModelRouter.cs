using System.Text.RegularExpressions;

namespace Phase4.ProductionAi.CostLatency;

public enum ModelTier { Canned, Small, Large }

// Where should this message go, and may its answer be cached?
//   Canned → "hi", "thanks": a fixed reply, NO model call (often 10–20% of chat traffic)
//   Small  → short, single factual questions: a small model is ~10× cheaper and faster
//   Large  → long, multi-part or reasoning questions ("compare", "why", several questions)
// Personal questions ("my order") are answered but NEVER cached: every user's answer differs and the data changes.
//
// Rules in code here (free, instant). Production: often a small model as the classifier, and the routing itself
// is proven with evals (golden questions: does the small model still pass?) before traffic moves to it.
public static partial class ModelRouter
{
    public record Decision(ModelTier Tier, bool Cacheable, string Reason);

    public static Decision Decide(string message)
    {
        var text = message.Trim().ToLowerInvariant();
        bool personal = Personal().IsMatch(text);

        if (Greeting().IsMatch(text)) return new(ModelTier.Canned, false, "greeting/thanks");
        if (text.Length > 200 || Complex().IsMatch(text) || text.Count(c => c == '?') > 1)
            return new(ModelTier.Large, !personal, "long, multi-part or reasoning");
        return new(ModelTier.Small, !personal, personal ? "simple, personal (not cached)" : "simple factual");
    }

    public static string CannedReply(string message) =>
        message.Contains("thank", StringComparison.OrdinalIgnoreCase) ? "You're welcome! Anything else?" : "Hi! How can I help you today?";

    [GeneratedRegex(@"^(hi|hello|hey|thanks|thank you|thx|ok|okay|bye)[!. ]*$")] private static partial Regex Greeting();
    [GeneratedRegex(@"\b(compare|why|explain|difference|versus|vs|pros and cons|step by step)\b")] private static partial Regex Complex();
    [GeneratedRegex(@"\b(my|mine|i ordered|i bought|my account)\b")] private static partial Regex Personal();
}
