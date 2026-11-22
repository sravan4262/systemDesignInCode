using System.Globalization;
using Npgsql;

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

    public async Task ReplaceDocumentAsync(DocumentInfo doc, List<Chunk> chunks, List<float[]> vectors, CancellationToken ct)
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
                INSERT INTO chunks (tenant_id, document_id, chunk_index, heading, content, allowed_groups, embedding_v1)
                VALUES (@t, @d, @i, @h, @c, @g, @e::vector)
                """, conn, tx);
            insert.Parameters.AddWithValue("t", doc.TenantId);
            insert.Parameters.AddWithValue("d", doc.DocumentId);
            insert.Parameters.AddWithValue("i", chunks[i].Index);
            insert.Parameters.AddWithValue("h", chunks[i].Heading);
            insert.Parameters.AddWithValue("c", chunks[i].Text);
            insert.Parameters.AddWithValue("g", doc.AllowedGroups);    // string[] → text[]
            insert.Parameters.AddWithValue("e", ToVector(vectors[i])); // "[0.12,-0.03,...]" → vector
            await insert.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);   // a crash before this line rolls everything back: the old chunks stay
    }

    // pgvector's text format. InvariantCulture: "0.12", never "0,12" on a German server.
    public static string ToVector(float[] v) =>
        "[" + string.Join(",", v.Select(x => x.ToString(CultureInfo.InvariantCulture))) + "]";
}
