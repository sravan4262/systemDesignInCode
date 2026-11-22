using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Phase4.Agents.Domain;

public record Order(string Id, string Owner, string Item, decimal Total, decimal Refunded, string Status);

// Orders + refunds in SQL Server (schema.sql). The tools call this; the model never does.
// Concurrency is handled by the database, not by the model behaving well:
//   - refunds.idempotency_key is the PK → the same refund runs once;
//   - the UPDATE only succeeds if enough is left to refund → two parallel refunds can't go over the total.
public class OrderStore(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Orders")!;

    public async Task<Order?> GetAsync(string orderId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, null, "SELECT id, owner, item, total, refunded, status FROM orders WHERE id = @o", ("o", orderId));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Read(r) : null;
    }

    public async Task<List<Order>> ListForAsync(string owner, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, null, "SELECT id, owner, item, total, refunded, status FROM orders WHERE owner = @u", ("u", owner));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var orders = new List<Order>();
        while (await r.ReadAsync(ct)) orders.Add(Read(r));
        return orders;
    }

    public async Task<string> RefundAsync(string idempotencyKey, string orderId, decimal amount, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        var result = await RefundInTxAsync(conn, tx, idempotencyKey, orderId, amount, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // Same request twice → same approval id (a stable hash) and only one row.
    public async Task<string> RequestApprovalAsync(string idempotencyKey, string orderId, decimal amount, string reason, string userId, CancellationToken ct)
    {
        var id = "apr-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)))[..12].ToLowerInvariant();
        await using var conn = await OpenAsync(ct);
        await using var cmd = Cmd(conn, null, """
            IF NOT EXISTS (SELECT 1 FROM pending_refunds WITH (UPDLOCK, HOLDLOCK) WHERE id = @id)
                INSERT INTO pending_refunds (id, order_id, amount, reason, requested_for) VALUES (@id, @o, @a, @r, @u)
            """, ("id", id), ("o", orderId), ("a", amount), ("r", reason), ("u", userId));
        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    // A human approves: remove the pending row AND run the refund in ONE transaction.
    // A double-click finds no pending row the second time → null → 404. A crash in between rolls both back.
    public async Task<string?> ApproveAsync(string approvalId, CancellationToken ct)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        string orderId; decimal amount;
        await using (var take = Cmd(conn, tx, "DELETE FROM pending_refunds OUTPUT deleted.order_id, deleted.amount WHERE id = @id", ("id", approvalId)))
        await using (var r = await take.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) return null;   // dispose rolls the transaction back
            (orderId, amount) = (r.GetString(0), r.GetDecimal(1));
        }

        var result = await RefundInTxAsync(conn, tx, approvalId, orderId, amount, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static async Task<string> RefundInTxAsync(SqlConnection conn, SqlTransaction tx, string key, string orderId, decimal amount, CancellationToken ct)
    {
        // 1. Already done? UPDLOCK + HOLDLOCK also locks the key when the row is MISSING, so a parallel
        //    call with the same key waits here instead of also refunding.
        await using (var done = Cmd(conn, tx, "SELECT result FROM refunds WITH (UPDLOCK, HOLDLOCK) WHERE idempotency_key = @k", ("k", key)))
            if (await done.ExecuteScalarAsync(ct) is string earlier) return earlier;

        // 2. Refund only if enough is left. The check and the write are ONE statement, so no race.
        await using (var update = Cmd(conn, tx, """
            UPDATE orders SET refunded = refunded + @a
            WHERE id = @o AND total - refunded >= @a
            """, ("a", amount), ("o", orderId)))
            if (await update.ExecuteNonQueryAsync(ct) == 0) return $"can't refund {amount}: not that much left on order {orderId}";

        // 3. Remember it under the key.
        var result = $"refunded {amount} on order {orderId}";
        await using (var insert = Cmd(conn, tx, "INSERT INTO refunds (idempotency_key, order_id, amount, result) VALUES (@k, @o, @a, @res)",
                                       ("k", key), ("o", orderId), ("a", amount), ("res", result)))
            await insert.ExecuteNonQueryAsync(ct);
        return result;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new SqlConnection(_connectionString);   // pooled: "new" reuses an open connection
        await conn.OpenAsync(ct);
        return conn;
    }

    private static SqlCommand Cmd(SqlConnection conn, SqlTransaction? tx, string sql, params (string Name, object Value)[] parameters)
    {
        var cmd = new SqlCommand(sql, conn, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }

    private static Order Read(SqlDataReader r) =>
        new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetString(5));
}
