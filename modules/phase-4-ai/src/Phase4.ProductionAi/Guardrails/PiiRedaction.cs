using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Phase4.ProductionAi.Guardrails;

// Mask personal data BEFORE the text reaches the model, the traces or the chat history.
public interface IPiiRedactor
{
    Task<PiiResult> RedactAsync(string text, CancellationToken ct);
}
public record PiiResult(string RedactedText, List<string> Found);   // Found = categories, never the values

// ---------------------------------------------------------------------------------------------
// Version 1, IN CODE: regex. Free, ~microseconds, deterministic, works offline.
// Misses anything without a fixed shape: names, addresses, "my card ends in four-one-one-one".
// ---------------------------------------------------------------------------------------------
public partial class RegexPiiRedactor : IPiiRedactor
{
    public Task<PiiResult> RedactAsync(string text, CancellationToken ct)
    {
        var found = new List<string>();

        // Cards first (before phone, which would also match long digit runs). Luhn check = fewer false hits
        // on order numbers and tracking ids that just happen to be long.
        text = Card().Replace(text, m => IsLuhnValid(m.Value) ? Mark(found, "CreditCard") : m.Value);
        text = Email().Replace(text, _ => Mark(found, "Email"));
        text = Phone().Replace(text, _ => Mark(found, "PhoneNumber"));

        return Task.FromResult(new PiiResult(text, found.Distinct().ToList()));
    }

    private static string Mark(List<string> found, string category) { found.Add(category); return $"[{category}]"; }

    // Luhn: double every second digit from the right; a real card number's total is a multiple of 10.
    private static bool IsLuhnValid(string raw)
    {
        var digits = raw.Where(char.IsDigit).Select(c => c - '0').Reverse().ToArray();
        int sum = digits.Select((d, i) => i % 2 == 1 ? (d * 2 > 9 ? d * 2 - 9 : d * 2) : d).Sum();
        return digits.Length is >= 13 and <= 19 && sum % 10 == 0;
    }

    [GeneratedRegex(@"\b(?:\d[ -]?){12,18}\d\b")] private static partial Regex Card();
    [GeneratedRegex(@"\b[\w.+-]+@[\w-]+\.[\w.-]+\b")] private static partial Regex Email();
    [GeneratedRegex(@"\+?\d[\d -]{7,}\d")] private static partial Regex Phone();
}

// ---------------------------------------------------------------------------------------------
// Version 2, BY CALLING AN ENDPOINT: Azure AI Language PII detection (a trained NER model).
// Finds names, addresses, IDs in many formats and languages; costs a network call (~100–300 ms) + per-call price.
// Key in user-secrets: dotnet user-secrets set "Azure:Language:Key" "..."  (endpoint in appsettings)
// ---------------------------------------------------------------------------------------------
public class AzurePiiRedactor(HttpClient http) : IPiiRedactor
{
    public async Task<PiiResult> RedactAsync(string text, CancellationToken ct)
    {
        var request = new
        {
            kind = "PiiEntityRecognition",
            parameters = new { modelVersion = "latest" },
            analysisInput = new { documents = new[] { new { id = "1", language = "en", text } } },
        };
        using var response = await http.PostAsJsonAsync("language/:analyze-text?api-version=2023-04-01", request, ct);
        response.EnsureSuccessStatusCode();   // a failure throws → the pipeline decides fail-open or fail-closed

        var body = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(ct);
        var doc = body!.Results.Documents[0];
        return new(doc.RedactedText, doc.Entities.Select(e => e.Category).Distinct().ToList());   // "Person", "Email", ...
    }

    private record AnalyzeResponse([property: JsonPropertyName("results")] AnalyzeResults Results);
    private record AnalyzeResults([property: JsonPropertyName("documents")] List<PiiDocument> Documents);
    private record PiiDocument(
        [property: JsonPropertyName("redactedText")] string RedactedText,      // values replaced with *****
        [property: JsonPropertyName("entities")] List<PiiEntity> Entities);
    private record PiiEntity([property: JsonPropertyName("category")] string Category);
}
