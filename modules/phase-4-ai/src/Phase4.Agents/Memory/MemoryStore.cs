using Microsoft.Data.SqlClient;

namespace Phase4.Agents.Memory;

public enum MemoryKind { ContactChannel, Language, DeliveryNote }   // a fixed list: the model can't invent new kinds
public record UserMemory(MemoryKind Kind, string Value);

// Long-term memory (user_memories): facts the customer stated about themselves, kept across chats.
// Scoped per user: every query has WHERE user_id = @u, with the user from the JWT.
public class MemoryStore(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Orders")!;

    public async Task<List<UserMemory>> GetAsync(string userId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT kind, value FROM user_memories WHERE user_id = @u", conn);
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var memories = new List<UserMemory>();
        while (await r.ReadAsync(ct)) memories.Add(new(Enum.Parse<MemoryKind>(r.GetString(0)), r.GetString(1)));
        return memories;
    }

    // Upsert: one value per kind. A newer statement replaces the older one.
    public async Task SaveAsync(string userId, MemoryKind kind, string value, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("""
            UPDATE user_memories WITH (UPDLOCK, HOLDLOCK) SET value = @v, updated_at = sysutcdatetime() WHERE user_id = @u AND kind = @k;
            IF @@ROWCOUNT = 0 INSERT INTO user_memories (user_id, kind, value) VALUES (@u, @k, @v);
            """, conn);
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("k", kind.ToString());
        cmd.Parameters.AddWithValue("v", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
