using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Phase4.Agents.Traces;

public record TraceSummary(int Runs, int Resolved, decimal TotalCostUsd, decimal CostPerResolvedUsd);

// Saves each trace as one JSON row (agent_traces in schema.sql), plus the few columns we filter and sum on.
// The trace holds the customer's message and tool results, so it is user data: owner check on read, ~30 days retention.
public class AgentTraceStore(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Orders")!;

    public async Task SaveAsync(AgentTrace trace, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            INSERT INTO agent_traces (id, user_id, stop_reason, cost_usd, trace)
            VALUES (@id, @u, @s, @cost, @trace)
            """, conn);
        cmd.Parameters.AddWithValue("id", trace.TraceId);
        cmd.Parameters.AddWithValue("u", trace.UserId);
        cmd.Parameters.AddWithValue("s", trace.StopReason);
        cmd.Parameters.AddWithValue("cost", trace.CostUsd);
        cmd.Parameters.AddWithValue("trace", JsonSerializer.Serialize(trace));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Raw JSON; null if it doesn't exist OR belongs to another user (don't reveal which).
    public async Task<string?> GetAsync(string userId, string traceId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT trace FROM agent_traces WHERE id = @id AND user_id = @u", conn);
        cmd.Parameters.AddWithValue("id", traceId);
        cmd.Parameters.AddWithValue("u", userId);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }

    // Cost per RESOLVED task = all spend (including runs that failed or handed off) ÷ runs that actually answered.
    // Failed runs still cost money, so this is the honest number, not "average cost per call".
    public async Task<TraceSummary> SummaryAsync(string userId, int days, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            SELECT COUNT(*),
                   SUM(CASE WHEN stop_reason = 'Answered' THEN 1 ELSE 0 END),
                   COALESCE(SUM(cost_usd), 0)
            FROM agent_traces
            WHERE user_id = @u AND created_at >= DATEADD(day, -@d, sysutcdatetime())
            """, conn);
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("d", days);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        int runs = r.GetInt32(0), resolved = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        decimal cost = r.GetDecimal(2);
        return new(runs, resolved, cost, resolved == 0 ? 0 : Math.Round(cost / resolved, 6));
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
