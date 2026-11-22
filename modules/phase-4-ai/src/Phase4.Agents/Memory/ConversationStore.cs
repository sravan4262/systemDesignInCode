using Microsoft.Data.SqlClient;

namespace Phase4.Agents.Memory;

public record Turn(long Seq, string Role, string Text);
public record ConversationHistory(string? Summary, List<Turn> Turns);   // Turns = only those NOT in the summary yet

// Short-term memory (conversations + conversation_messages): the turns of one chat.
public class ConversationStore(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Orders")!;

    // Someone else's conversation id → empty history (same as a new chat): reveals nothing, mixes nothing.
    public async Task<ConversationHistory> GetAsync(string conversationId, string userId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        string? summary = null;
        long upTo = 0;
        await using (var cmd = new SqlCommand("SELECT summary, summarized_up_to FROM conversations WHERE id = @c AND user_id = @u", conn))
        {
            cmd.Parameters.AddWithValue("c", conversationId);
            cmd.Parameters.AddWithValue("u", userId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return new(null, []);
            (summary, upTo) = (r.IsDBNull(0) ? null : r.GetString(0), r.GetInt64(1));
        }

        await using var turnsCmd = new SqlCommand(
            "SELECT seq, role, text FROM conversation_messages WHERE conversation_id = @c AND seq > @upTo ORDER BY seq", conn);
        turnsCmd.Parameters.AddWithValue("c", conversationId);
        turnsCmd.Parameters.AddWithValue("upTo", upTo);
        await using var tr = await turnsCmd.ExecuteReaderAsync(ct);
        var turns = new List<Turn>();
        while (await tr.ReadAsync(ct)) turns.Add(new(tr.GetInt64(0), tr.GetString(1), tr.GetString(2)));
        return new(summary, turns);
    }

    // Saves one turn (the user's message + the agent's answer); creates the conversation on the first turn.
    public async Task AppendTurnAsync(string conversationId, string userId, string userText, string assistantText, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        await using (var create = new SqlCommand("""
            IF NOT EXISTS (SELECT 1 FROM conversations WITH (UPDLOCK, HOLDLOCK) WHERE id = @c)
                INSERT INTO conversations (id, user_id) VALUES (@c, @u)
            """, conn, tx))
        {
            create.Parameters.AddWithValue("c", conversationId);
            create.Parameters.AddWithValue("u", userId);
            await create.ExecuteNonQueryAsync(ct);
        }
        foreach (var (role, text) in new[] { ("user", userText), ("assistant", assistantText) })
        {
            // Only into the user's OWN conversation: someone else's id writes nothing.
            // (Real app: the server creates conversation ids as GUIDs, so they can't be guessed.)
            await using var insert = new SqlCommand("""
                INSERT INTO conversation_messages (conversation_id, role, text)
                SELECT @c, @r, @t WHERE EXISTS (SELECT 1 FROM conversations WHERE id = @c AND user_id = @u)
                """, conn, tx);
            insert.Parameters.AddWithValue("c", conversationId);
            insert.Parameters.AddWithValue("u", userId);
            insert.Parameters.AddWithValue("r", role);
            insert.Parameters.AddWithValue("t", text);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task SaveSummaryAsync(string conversationId, string summary, long upToSeq, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new SqlCommand("UPDATE conversations SET summary = @s, summarized_up_to = @upTo WHERE id = @c", conn);
        cmd.Parameters.AddWithValue("s", summary);
        cmd.Parameters.AddWithValue("upTo", upToSeq);
        cmd.Parameters.AddWithValue("c", conversationId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
