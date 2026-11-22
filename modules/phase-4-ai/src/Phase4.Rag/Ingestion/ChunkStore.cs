using System.Globalization;
using Npgsql;
using NpgsqlTypes;

namespace Phase4.Rag.Ingestion;

public record DocumentInfo(string TenantId, string DocumentId, string Title, string Url, string[] AllowedGroups, string ContentHash);

// Reads and writes the documents + chunks tables (schema.sql).
// A document's chunks are replaced in ONE transaction: a search sees all old chunks or all new ones, never a mix.
public class ChunkStore(NpgsqlDataSource db)
{
    public async Task<string?> GetHashAsync(string tenantId, string documentId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("SELECT content_hash FROM documents WHERE tenant_id = @t AND id = @d");
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("d", documentId);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    // ---------- ingestion ----------

    // vectorsV2 is null unless a model upgrade is in progress (dual write).
    public async Task ReplaceDocumentAsync(DocumentInfo doc, List<Chunk> chunks, List<float[]> vectorsV1, List<float[]>? vectorsV2, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // Upsert the document row first: it also LOCKS the row, so two uploads of the same document run one after the other.
        await using (var upsert = new NpgsqlCommand("""
            INSERT INTO documents (tenant_id, id, title, url, content_hash) VALUES (@t, @d, @title, @url, @hash)
            ON CONFLICT (tenant_id, id) DO UPDATE SET title = @title, url = @url, content_hash = @hash, updated_at = now()
            """, conn, tx))
        {
            upsert.Parameters.AddWithValue("t", doc.TenantId);
            upsert.Parameters.AddWithValue("d", doc.DocumentId);
            upsert.Parameters.AddWithValue("title", doc.Title);
            upsert.Parameters.AddWithValue("url", doc.Url);
            upsert.Parameters.AddWithValue("hash", doc.ContentHash);
            await upsert.ExecuteNonQueryAsync(ct);
        }

        await using (var delete = new NpgsqlCommand("DELETE FROM chunks WHERE tenant_id = @t AND document_id = @d", conn, tx))
        {
            delete.Parameters.AddWithValue("t", doc.TenantId);
            delete.Parameters.AddWithValue("d", doc.DocumentId);
            await delete.ExecuteNonQueryAsync(ct);
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO chunks (tenant_id, document_id, chunk_index, heading, content, allowed_groups, embedding_v1, embedding_v2)
                VALUES (@t, @d, @i, @h, @c, @g, @e1::vector, @e2::vector)
                """, conn, tx);
            insert.Parameters.AddWithValue("t", doc.TenantId);
            insert.Parameters.AddWithValue("d", doc.DocumentId);
            insert.Parameters.AddWithValue("i", chunks[i].Index);
            insert.Parameters.AddWithValue("h", chunks[i].Heading);
            insert.Parameters.AddWithValue("c", chunks[i].Text);
            insert.Parameters.AddWithValue("g", doc.AllowedGroups);      // string[] → text[]
            insert.Parameters.AddWithValue("e1", ToVector(vectorsV1[i]));  // "[0.12,-0.03,...]" → vector
            insert.Parameters.Add("e2", NpgsqlDbType.Text).Value = vectorsV2 is null ? DBNull.Value : ToVector(vectorsV2[i]);
            await insert.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);   // a crash before this line rolls everything back: the old chunks stay
    }

    // ---------- freshness ----------

    // ON DELETE CASCADE removes the chunks in the same statement. Returns false if the document didn't exist.
    public async Task<bool> DeleteDocumentAsync(string tenantId, string documentId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("DELETE FROM documents WHERE tenant_id = @t AND id = @d");
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("d", documentId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // Permissions change → plain UPDATE, no re-embedding: the text didn't change, only who may see it.
    public async Task<bool> UpdatePermissionsAsync(string tenantId, string documentId, string[] groups, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("UPDATE chunks SET allowed_groups = @g WHERE tenant_id = @t AND document_id = @d");
        cmd.Parameters.AddWithValue("g", groups);
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("d", documentId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ---------- model upgrade backfill ----------

    public async Task<List<(long Id, string Content)>> GetChunksMissingV2Async(int limit, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("SELECT id, content FROM chunks WHERE embedding_v2 IS NULL ORDER BY id LIMIT @n");
        cmd.Parameters.AddWithValue("n", limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<(long, string)>();
        while (await reader.ReadAsync(ct)) rows.Add((reader.GetInt64(0), reader.GetString(1)));
        return rows;
    }

    // A chunk deleted or replaced meanwhile just updates 0 rows: no harm.
    public async Task SetV2Async(List<(long Id, float[] Vector)> rows, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        foreach (var (id, vector) in rows)
        {
            await using var cmd = new NpgsqlCommand("UPDATE chunks SET embedding_v2 = @e::vector WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("e", ToVector(vector));
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // pgvector's text format. InvariantCulture: "0.12", never "0,12" on a German server.
    public static string ToVector(float[] v) =>
        "[" + string.Join(",", v.Select(x => x.ToString(CultureInfo.InvariantCulture))) + "]";
}
