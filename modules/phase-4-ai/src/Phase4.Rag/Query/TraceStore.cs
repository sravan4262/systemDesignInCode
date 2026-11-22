using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Phase4.Rag.Query;

// Saves each trace as one JSON row (rag_traces in schema.sql). Kept ~30 days: enough to debug a complaint.
// The trace holds the question and answer, so it is user data: same tenant check and retention rules as the documents.
public class TraceStore(NpgsqlDataSource db)
{
    public async Task SaveAsync(RagTrace trace, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("INSERT INTO rag_traces (id, tenant_id, trace) VALUES (@id, @t, @trace)");
        cmd.Parameters.AddWithValue("id", trace.TraceId);
        cmd.Parameters.AddWithValue("t", trace.TenantId);
        cmd.Parameters.Add("trace", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(trace);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Returns the raw JSON; null if it doesn't exist OR belongs to another tenant (don't reveal which).
    public async Task<string?> GetAsync(string tenantId, string traceId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("SELECT trace::text FROM rag_traces WHERE tenant_id = @t AND id = @id");
        cmd.Parameters.AddWithValue("t", tenantId);
        cmd.Parameters.AddWithValue("id", traceId);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }
}
