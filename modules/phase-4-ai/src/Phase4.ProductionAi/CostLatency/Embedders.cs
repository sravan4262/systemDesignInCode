using Microsoft.Extensions.AI;

namespace Phase4.ProductionAi.CostLatency;

// Turns a question into a vector so "similar meaning" = "close vectors" (cosine similarity).
public interface ITextEmbedder
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}

// Version 1, IN CODE: hashed bag of words. Free, offline, crude: "refund policy" ≈ "policy on refunds" only if
// they share words; it knows nothing about synonyms ("money back"). Good enough to SEE the cache work.
public class LocalHashEmbedder : ITextEmbedder
{
    private const int Dims = 256;
    private static readonly HashSet<string> StopWords =
        ["what", "is", "your", "the", "a", "an", "how", "does", "do", "i", "you", "can", "of", "for", "to", "in", "on", "are", "please", "tell", "me", "about", "whats"];

    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var v = new float[Dims];
        var words = new string(text.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !StopWords.Contains(w))
            .Select(w => w.TrimEnd('s'));                      // "refunds" ≈ "refund" (very rough stemming), AFTER stop words ("does" ≠ "doe")
        foreach (var w in words) v[(int)((uint)StringComparer.Ordinal.GetHashCode(w) % Dims)] += 1;   // per-process hash: fine for an in-memory cache
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        return Task.FromResult(norm == 0 ? v : v.Select(x => x / norm).ToArray());
    }
}

// Version 2, BY CALLING AN ENDPOINT: a real embedding model (OpenAI text-embedding-3-small).
// Understands meaning ("money back" ≈ "refund"); costs a call (~20–50 ms, tiny price).
public class OpenAiEmbedder(IEmbeddingGenerator<string, Embedding<float>> generator) : ITextEmbedder
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct) =>
        (await generator.GenerateAsync(text, cancellationToken: ct)).Vector.ToArray();
}
