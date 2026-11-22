using Microsoft.Data.SqlClient;

namespace Phase4.Agents.Domain;

public enum CaseStatus { Open, WaitingForApproval, Resolved }
public record SupportCase(string Id, CaseStatus Status, string Notes, int Version);

// A support case shared by several agents running at the same time (support_cases in schema.sql).
// Optimistic concurrency: no locks while agents think; each write says "only if it's still the version I read".
public class CaseStore(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Orders")!;

    public async Task EnsureAsync(string caseId, string userId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            IF NOT EXISTS (SELECT 1 FROM support_cases WITH (UPDLOCK, HOLDLOCK) WHERE id = @c)
                INSERT INTO support_cases (id, user_id) VALUES (@c, @u)
            """, conn);
        cmd.Parameters.AddWithValue("c", caseId);
        cmd.Parameters.AddWithValue("u", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<SupportCase?> GetAsync(string caseId, string userId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT id, status, notes, version FROM support_cases WHERE id = @c AND user_id = @u", conn);
        cmd.Parameters.AddWithValue("c", caseId);
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)
            ? new(r.GetString(0), Enum.Parse<CaseStatus>(r.GetString(1)), r.GetString(2), r.GetInt32(3))
            : null;
    }

    // true = written (version is now expectedVersion + 1); false = someone else wrote first → re-read and decide again.
    public async Task<bool> TryUpdateAsync(string caseId, string userId, int expectedVersion, CaseStatus status, string notes, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            UPDATE support_cases SET status = @s, notes = @n, version = version + 1
            WHERE id = @c AND user_id = @u AND version = @v
            """, conn);
        cmd.Parameters.AddWithValue("s", status.ToString());
        cmd.Parameters.AddWithValue("n", notes);
        cmd.Parameters.AddWithValue("c", caseId);
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("v", expectedVersion);
        return await cmd.ExecuteNonQueryAsync(ct) == 1;   // the check and the write are ONE statement: no race
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
