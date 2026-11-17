using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace M06.Databases;

// Read/write split: writes → the ONE primary; reads → a replica, unless the user just wrote (read-your-writes).
public enum DbIntent { Write, Read }

// 1) SETUP: connection strings come from config. The primary is a LISTENER name: on failover the
//    cluster re-points it to the new primary, so this string never changes.
public record DbRoutingOptions(
    string Primary,                    // "Server=shop-listener;Database=Shop;MultiSubnetFailover=True"
    IReadOnlyList<string> Replicas,    // "Server=shop-replica-1;..." , "Server=shop-replica-2;..."
    TimeSpan ReadYourWritesWindow);    // e.g. 5 s: after a write, this user reads from the primary

// 2) RUNTIME DECISION: the caller states its intent; the factory picks the server.
public class DbConnectionFactory(DbRoutingOptions options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastWriteByUser = new();
    private int _next = -1;

    public async Task<SqlConnection> OpenAsync(DbIntent intent, string? userId = null)
    {
        var connectionString = Pick(intent, userId);
        var connection = new SqlConnection(connectionString);   // pooled: borrows an open connection
        await connection.OpenAsync();
        return connection;
    }

    // Call after a successful write so this user's next reads see it.
    public void RecordWrite(string userId) => _lastWriteByUser[userId] = clock.GetUtcNow();

    private string Pick(DbIntent intent, string? userId)
    {
        if (intent == DbIntent.Write) return options.Primary;                      // all writes → primary

        if (userId is not null &&
            _lastWriteByUser.TryGetValue(userId, out var lastWrite) &&
            clock.GetUtcNow() - lastWrite < options.ReadYourWritesWindow)
            return options.Primary;                                                // just wrote → primary

        if (options.Replicas.Count == 0) return options.Primary;                   // no replicas → primary

        var i = (int)((uint)Interlocked.Increment(ref _next) % (uint)options.Replicas.Count);
        return options.Replicas[i];                                                // round-robin replicas
    }
}
// Simpler alternative (most common in practice): ONE connection string to the listener with
// "ApplicationIntent=ReadOnly" for reads; SQL Server's read-only routing picks the replica for you.
// Note: this tracker is per app instance; with several instances keep it in a cookie/claim instead.

// 3) USAGE: each operation declares what it needs.
public class OrderService(DbConnectionFactory db)
{
    public async Task PlaceOrderAsync(string userId, string sku)
    {
        await using var conn = await db.OpenAsync(DbIntent.Write);                 // → primary
        await using var cmd = new SqlCommand(
            "UPDATE Products SET Stock = Stock - 1 WHERE Sku = @sku AND Stock >= 1", conn);
        cmd.Parameters.AddWithValue("@sku", sku);
        await cmd.ExecuteNonQueryAsync();
        db.RecordWrite(userId);
    }

    public async Task<object?> GetMyLatestOrderAsync(string userId)
    {
        await using var conn = await db.OpenAsync(DbIntent.Read, userId);          // → primary if just wrote, else replica
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 Id FROM Orders WHERE UserId = @u ORDER BY Id DESC", conn);
        cmd.Parameters.AddWithValue("@u", userId);
        return await cmd.ExecuteScalarAsync();
    }

    public async Task<object?> CountProductsAsync()
    {
        await using var conn = await db.OpenAsync(DbIntent.Read);                  // → any replica (staleness OK)
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM Products", conn);
        return await cmd.ExecuteScalarAsync();
    }
}
