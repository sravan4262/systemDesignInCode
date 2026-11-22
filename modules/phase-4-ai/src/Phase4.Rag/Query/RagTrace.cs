using System.Diagnostics;

namespace Phase4.Rag.Query;

// One record per question: what every stage saw. "Wrong answer" → open the trace and find the stage
// where the right chunk disappeared:
//   not in Keyword or Vector  → retrieval (chunking, embedding, filters)
//   in them but not in Rrf    → fusion / topK too small
//   low rerank score          → reranker, or the chunk doesn't really answer it
//   in PromptSources but answer still wrong → prompt / model
public class RagTrace
{
    public string TraceId { get; } = Guid.NewGuid().ToString("N");
    public required string TenantId { get; init; }
    public required string Question { get; init; }
    public string PromptVersion { get; set; } = "";
    public string Model { get; set; } = "";
    public string EmbeddingVersion { get; set; } = "";

    public List<long> KeywordIds { get; set; } = [];
    public List<long> VectorIds { get; set; } = [];
    public List<long> RrfIds { get; set; } = [];
    public record ScoredChunk(long ChunkId, double Score);
    public List<ScoredChunk> RerankScores { get; set; } = [];                 // ALL of them, also those below MinScore
    public List<long> PromptSourceIds { get; set; } = [];                    // what the model actually saw, in order

    public string RawAnswer { get; set; } = "";                 // before CitationChecker
    public string Answer { get; set; } = "";                    // after: invented [n] removed
    public long InputTokens { get; set; }
    public long CachedInputTokens { get; set; }                 // prompt-cache hits (fixed-first order pays off here)
    public long OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public Dictionary<string, long> StageMs { get; } = [];     // "search": 120, "rerank": 180, "llm": 2400

    // using (trace.Time("rerank")) { ... }  → records how long the block took
    public IDisposable Time(string stage) => new StageTimer(this, stage);

    private sealed class StageTimer(RagTrace trace, string stage) : IDisposable
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        public void Dispose() => trace.StageMs[stage] = _watch.ElapsedMilliseconds;
    }

    // gpt-4.1-mini, USD per 1M tokens. Cached input is billed at a discount; output costs ~4× input.
    private const decimal InputPerM = 0.40m, CachedPerM = 0.10m, OutputPerM = 1.60m;

    public void SetUsage(long input, long cached, long output)
    {
        (InputTokens, CachedInputTokens, OutputTokens) = (input, cached, output);
        CostUsd = ((input - cached) * InputPerM + cached * CachedPerM + output * OutputPerM) / 1_000_000m;
    }
}
