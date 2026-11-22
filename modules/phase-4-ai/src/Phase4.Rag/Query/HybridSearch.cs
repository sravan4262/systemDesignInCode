using Npgsql;
using Phase4.Rag.Ingestion;

namespace Phase4.Rag.Query;

public record SearchHit(long ChunkId, string DocumentId, string Title, string Url, string Heading, string Content);

// Two searches over the same chunks, both filtered by tenant + the user's groups, then merged with RRF.
// Keyword finds exact words (error codes, product names); vector finds the same meaning in other words.
public class HybridSearch(NpgsqlDataSource db, EmbeddingVersions models)
{
    private const string Select = """
        SELECT c.id, c.document_id, d.title, d.url, c.heading, c.content
        FROM chunks c JOIN documents d ON d.tenant_id = c.tenant_id AND d.id = c.document_id
        WHERE c.tenant_id = @t AND c.allowed_groups && @g
        """;   // && = "arrays overlap": the user is in at least one allowed group

    public async Task<List<SearchHit>> SearchAsync(string question, string tenantId, string[] userGroups, int topK, RagTrace trace, CancellationToken ct)
    {
        trace.EmbeddingVersion = models.QueryVersion;
        // Same model (and column) for the question as for the chunks, or the vectors can't be compared.
        var questionVector = (await models.EmbedAsync([question], models.QueryVersion, ct))[0];

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // HNSW filters AFTER finding the nearest rows: with strict filters, 40 neighbours might leave 0 rows.
        // iterative_scan keeps walking the index until enough rows pass the WHERE.
        await using (var set = new NpgsqlCommand("SET LOCAL hnsw.iterative_scan = strict_order", conn, tx))
            await set.ExecuteNonQueryAsync(ct);

        var keyword = await QueryAsync(conn, tx, $"""
            {Select} AND c.content_tsv @@ websearch_to_tsquery('english', @q)
            ORDER BY ts_rank(c.content_tsv, websearch_to_tsquery('english', @q)) DESC LIMIT @k
            """, ct, ("t", tenantId), ("g", userGroups), ("q", question), ("k", topK));

        var vector = await QueryAsync(conn, tx, $"""
            {Select} ORDER BY c.{models.QueryColumn} <=> @e::vector LIMIT @k
            """, ct, ("t", tenantId), ("g", userGroups), ("e", ChunkStore.ToVector(questionVector)), ("k", topK));
        // <=> = cosine distance (0 = same direction). QueryColumn is "embedding_v1"/"embedding_v2", never user input.

        var fused = Rrf(topK, keyword, vector);

        trace.KeywordIds = keyword.Select(h => h.ChunkId).ToList();   // each list separately: shows WHICH search missed
        trace.VectorIds = vector.Select(h => h.ChunkId).ToList();
        trace.RrfIds = fused.Select(h => h.ChunkId).ToList();
        return fused;
    }

    // Reciprocal Rank Fusion: score = Σ 1 / (60 + rank) over both lists. Uses positions only, so a ts_rank
    // and a cosine distance never have to be compared. A chunk found by BOTH searches adds up and rises to the top.
    public static List<SearchHit> Rrf(int topK, params List<SearchHit>[] lists) =>
        lists.SelectMany(list => list.Select((hit, i) => (hit, score: 1.0 / (60 + i + 1))))
             .GroupBy(x => x.hit.ChunkId)
             .Select(g => (g.First().hit, score: g.Sum(x => x.score)))
             .OrderByDescending(x => x.score)
             .Take(topK)
             .Select(x => x.hit)
             .ToList();

    private static async Task<List<SearchHit>> QueryAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql,
        CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var hits = new List<SearchHit>();
        while (await reader.ReadAsync(ct))
            hits.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return hits;
    }
}
