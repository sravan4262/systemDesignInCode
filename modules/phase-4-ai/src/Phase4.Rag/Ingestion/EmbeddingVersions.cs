using Microsoft.Extensions.AI;

namespace Phase4.Rag.Ingestion;

// Blue/green embedding model upgrade. v1 and v2 vectors come from different models, so they can't be compared:
// a question embedded with v2 must be searched against embedding_v2 only.
//   1. Dual write: Embeddings:DualWrite = true → new uploads get BOTH vectors.
//   2. Backfill:   EmbeddingBackfill fills embedding_v2 for the old rows.
//   3. Switch:     Embeddings:QueryVersion = "v2" → search uses embedding_v2 (instant, and instant to roll back).
//   4. Clean up:   stop writing v1, drop the column.
public class EmbeddingVersions(
    IConfiguration config,
    [FromKeyedServices("v1")] IEmbeddingGenerator<string, Embedding<float>> v1,
    [FromKeyedServices("v2")] IEmbeddingGenerator<string, Embedding<float>> v2)
{
    public bool DualWrite => config.GetValue<bool>("Embeddings:DualWrite");      // read on every call: a config change applies live
    public string QueryVersion => config["Embeddings:QueryVersion"] ?? "v1";
    public string QueryColumn => QueryVersion == "v2" ? "embedding_v2" : "embedding_v1";   // a fixed name, never user input

    public async Task<List<float[]>> EmbedAsync(IEnumerable<string> texts, string version, CancellationToken ct)
    {
        // v2 is text-embedding-3-large shortened to 1536 numbers: a pgvector HNSW index allows at most 2,000.
        var (model, options) = version == "v2" ? (v2, new EmbeddingGenerationOptions { Dimensions = 1536 }) : (v1, null);
        var result = await model.GenerateAsync(texts, options, ct);   // one batched API call for all texts
        return result.Select(e => e.Vector.ToArray()).ToList();
    }
}
