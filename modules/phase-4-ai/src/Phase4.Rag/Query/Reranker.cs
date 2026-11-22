using System.Text.Json;

namespace Phase4.Rag.Query;

public record RankedHit(SearchHit Hit, double Score);

// A cross-encoder: a separate model that reads the question and ONE chunk together and scores how well the
// chunk answers it (0–1). Too slow for millions of chunks, fine for the 20 that hybrid search found.
// Called over HTTP (Cohere's rerank API here; Azure AI Search has it built in as semantic ranking).
public class Reranker(HttpClient http)
{
    public const double MinScore = 0.3;   // below this no chunk really answers → say "I don't know" (tune on real questions)

    private record RerankRequest(string Model, string Query, string[] Documents, int TopN);
    private record RerankResponse(List<RerankResult> Results);
    private record RerankResult(int Index, double RelevanceScore);
    private static readonly JsonSerializerOptions SnakeCase = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<List<RankedHit>> RerankAsync(string question, List<SearchHit> hits, int topN, CancellationToken ct)
    {
        if (hits.Count == 0) return [];

        var request = new RerankRequest("rerank-v3.5", question, hits.Select(h => h.Content).ToArray(), topN);
        using var response = await http.PostAsJsonAsync("v2/rerank", request, SnakeCase, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RerankResponse>(SnakeCase, ct);

        // The API returns positions in the list we sent, best first.
        return result!.Results.Select(r => new RankedHit(hits[r.Index], r.RelevanceScore)).ToList();
    }
}
