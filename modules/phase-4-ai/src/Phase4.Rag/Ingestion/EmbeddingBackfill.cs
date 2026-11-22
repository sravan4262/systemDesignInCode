namespace Phase4.Rag.Ingestion;

// Step 2 of the model upgrade: fills embedding_v2 for chunks written before dual write was turned on.
// Batches of 100: a failure loses at most one batch. Safe to restart: it only picks rows where embedding_v2 IS NULL.
public class EmbeddingBackfill(ChunkStore store, EmbeddingVersions models, ILogger<EmbeddingBackfill> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!models.DualWrite) return;   // no upgrade in progress

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var batch = await store.GetChunksMissingV2Async(100, ct);
                if (batch.Count == 0)
                {
                    log.LogInformation("v2 backfill done: safe to set Embeddings:QueryVersion = v2");
                    return;
                }
                var vectors = await models.EmbedAsync(batch.Select(c => c.Content), "v2", ct);
                await store.SetV2Async(batch.Select(c => c.Id).Zip(vectors).ToList(), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "v2 backfill batch failed, retrying in 30 s");   // e.g. a 429 from the embedding API
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }
}
