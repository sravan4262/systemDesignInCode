using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Phase4.ProductionAi.Guardrails;

// Harmful-content check: a score 0–1 per category. The PIPELINE applies the thresholds, not the moderator,
// so both versions are judged by the same review/block lines.
public interface IModerator
{
    Task<Dictionary<string, double>> ScoreAsync(string text, CancellationToken ct);
}

// ---------------------------------------------------------------------------------------------
// Version 1, IN CODE: a word list. Free and instant, but crude in both directions:
//   misses: paraphrases, spelling tricks ("k1ll"), other languages;
//   false hits: "how do I kill the stuck process?".
// Good as a cheap first filter and as the FALLBACK when the service is down.
// ---------------------------------------------------------------------------------------------
public class KeywordModerator : IModerator
{
    private static readonly (string Phrase, string Category, double Score)[] Rules =
    [
        ("kill you", "Violence", 0.95),
        ("bomb", "Violence", 0.9),
        ("hurt myself", "SelfHarm", 0.95),
        ("idiot", "Harassment", 0.6),     // rude, not dangerous → lands in the review band
        ("stupid", "Harassment", 0.5),
    ];

    public Task<Dictionary<string, double>> ScoreAsync(string text, CancellationToken ct)
    {
        var lower = text.ToLowerInvariant();
        var scores = Rules.Where(r => lower.Contains(r.Phrase))
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Max(r => r.Score));
        return Task.FromResult(scores);
    }
}

// ---------------------------------------------------------------------------------------------
// Version 2, BY CALLING AN ENDPOINT: Azure AI Content Safety (a trained classifier per category).
// Understands meaning and context ("kill the process" is fine); costs a call + price per 1k texts.
// Returns severity 0/2/4/6 → mapped to 0–1 so the same thresholds work for both versions.
// Key in user-secrets: dotnet user-secrets set "Azure:ContentSafety:Key" "..."
// ---------------------------------------------------------------------------------------------
public class AzureContentSafetyModerator(HttpClient http) : IModerator
{
    public async Task<Dictionary<string, double>> ScoreAsync(string text, CancellationToken ct)
    {
        var request = new { text, categories = new[] { "Hate", "SelfHarm", "Sexual", "Violence" }, outputType = "FourSeverityLevels" };
        using var response = await http.PostAsJsonAsync("contentsafety/text:analyze?api-version=2024-09-01", request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(ct);
        return body!.CategoriesAnalysis.ToDictionary(c => c.Category, c => c.Severity / 6.0);
    }

    private record AnalyzeResponse([property: JsonPropertyName("categoriesAnalysis")] List<CategoryResult> CategoriesAnalysis);
    private record CategoryResult(
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("severity")] int Severity);
}
