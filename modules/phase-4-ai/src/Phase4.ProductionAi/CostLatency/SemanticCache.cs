using System.Collections.Concurrent;

namespace Phase4.ProductionAi.CostLatency;

// A cache of ANSWERS for SIMILAR questions: "what's your refund policy?" ≈ "how does the refund policy work"
// → return the stored answer, no model call (cost 0, ~ms instead of seconds).
//
// The trap: the key is NOT just the question vector. Two users asking the same words may be allowed to see
// different documents, and documents change. So entries live in a SCOPE, matched exactly before similarity:
//   scope = tenant | groups (sorted) | knowledge-base version
// → acme/support never gets acme/hr's answer, globex never gets acme's, and a document change starts a new
//   scope, so old answers are simply never found again (invalidation by key, like a version in a cache key).
public class SemanticCache(double threshold, TimeSpan ttl)
{
    private record Entry(float[] Vector, string Question, string Answer, DateTimeOffset At);
    private readonly ConcurrentDictionary<string, List<Entry>> _byScope = new();
    private int _kbVersion = 1;

    public int KnowledgeBaseVersion => _kbVersion;
    public void DocumentsChanged() => Interlocked.Increment(ref _kbVersion);   // real app: on ingestion (outbox event)

    public string Scope(string tenantId, IEnumerable<string> groups) =>
        $"{tenantId}|{string.Join(",", groups.Order())}|kb{_kbVersion}";

    public (string Answer, string MatchedQuestion, double Similarity)? Find(string scope, float[] vector)
    {
        if (!_byScope.TryGetValue(scope, out var entries)) return null;
        lock (entries)
        {
            var best = entries
                .Where(e => DateTimeOffset.UtcNow - e.At < ttl)
                .Select(e => (e, sim: Cosine(e.Vector, vector)))
                .OrderByDescending(x => x.sim)
                .FirstOrDefault();
            return best.e is not null && best.sim >= threshold ? (best.e.Answer, best.e.Question, Math.Round(best.sim, 3)) : null;
        }
    }

    public void Store(string scope, float[] vector, string question, string answer)
    {
        var entries = _byScope.GetOrAdd(scope, _ => []);
        lock (entries)
        {
            entries.Add(new(vector, question, answer, DateTimeOffset.UtcNow));
            if (entries.Count > 1000) entries.RemoveAt(0);   // bounded: oldest out (real app: Redis + TTL/LRU, M8)
        }
    }

    // Vectors are normalised, so the dot product = cosine similarity (1 = same direction, 0 = unrelated).
    private static double Cosine(float[] a, float[] b) => a.Zip(b, (x, y) => (double)x * y).Sum();
}
