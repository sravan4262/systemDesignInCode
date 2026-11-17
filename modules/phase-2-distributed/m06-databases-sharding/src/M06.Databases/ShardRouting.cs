using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;

namespace M06.Databases;

// Directory-based sharding: a lookup table says which shard each tenant lives on.
// Shard key = TenantId (it's in almost every query). Each shard = a full SQL Server, same schema.

// 1) SETUP: the shards and their connection strings come from config.
public record ShardMapOptions(
    IReadOnlyDictionary<string, string> Shards,   // "shard-1" → "Server=sql-shard-1;Database=Orders;..."
    TimeSpan DirectoryCacheTtl);                  // e.g. 1 min: how long a tenant → shard answer is cached

// 2) THE DIRECTORY: tenantId → shardId. Production: a small table in a separate, replicated
//    "catalog" database:  CREATE TABLE TenantShards (TenantId INT PRIMARY KEY, ShardId VARCHAR(20));
public interface IShardDirectory
{
    Task<string?> FindShardAsync(int tenantId);
    Task AssignAsync(int tenantId, string shardId);
    Task<IReadOnlyDictionary<string, int>> TenantCountPerShardAsync();
}

public class InMemoryShardDirectory : IShardDirectory
{
    private readonly ConcurrentDictionary<int, string> _map = new();

    public Task<string?> FindShardAsync(int tenantId) =>
        Task.FromResult(_map.TryGetValue(tenantId, out var shard) ? shard : null);

    public Task AssignAsync(int tenantId, string shardId)
    {
        _map[tenantId] = shardId;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, int>> TenantCountPerShardAsync() =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(
            _map.Values.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count()));
}

// 3) THE RESOLVER: tenantId → shard, with a short cache so we don't hit the directory on every query.
public class ShardResolver(IShardDirectory directory, ShardMapOptions options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<int, (string ShardId, DateTimeOffset CachedAt)> _cache = new();

    public async Task<string> ResolveAsync(int tenantId)
    {
        if (_cache.TryGetValue(tenantId, out var hit) && clock.GetUtcNow() - hit.CachedAt < options.DirectoryCacheTtl)
            return hit.ShardId;                                                     // fast path: cached

        var shardId = await directory.FindShardAsync(tenantId)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} is not assigned to a shard.");

        _cache[tenantId] = (shardId, clock.GetUtcNow());
        return shardId;
    }

    // Onboarding: put a NEW tenant on the shard with the fewest tenants (simple placement policy).
    public async Task<string> AssignNewTenantAsync(int tenantId)
    {
        var counts = await directory.TenantCountPerShardAsync();
        var shardId = options.Shards.Keys.MinBy(s => counts.GetValueOrDefault(s))!;
        await directory.AssignAsync(tenantId, shardId);
        return shardId;
    }

    // Call after moving a tenant (Session 9) so this instance stops using the old shard right away.
    // Other app instances still have the old answer until their TTL expires. Session 9 deals with that.
    public void Invalidate(int tenantId) => _cache.TryRemove(tenantId, out _);
}

// 4) RUNTIME: callers ask for a connection FOR A TENANT; they never pick a server.
public class ShardedConnectionFactory(ShardResolver resolver, ShardMapOptions options)
{
    public async Task<SqlConnection> OpenForTenantAsync(int tenantId)
    {
        var shardId = await resolver.ResolveAsync(tenantId);
        var connection = new SqlConnection(options.Shards[shardId]);
        await connection.OpenAsync();
        return connection;
    }

    // Only for the rare cross-tenant query: one connection per shard.
    public IEnumerable<string> AllShardConnectionStrings => options.Shards.Values;
}

// 5) USAGE
public class ShardedOrderQueries(ShardedConnectionFactory shards)
{
    // ✅ Single-shard: TenantId is known (from the user's token), so exactly ONE shard is queried.
    public async Task<int> CountOrdersLastWeekAsync(int tenantId)
    {
        await using var conn = await shards.OpenForTenantAsync(tenantId);
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM Orders WHERE TenantId = @t AND CreatedAt >= DATEADD(day, -7, SYSUTCDATETIME())", conn);
        cmd.Parameters.AddWithValue("@t", tenantId);   // still filter by TenantId: a shard holds MANY tenants
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    // ❌ Scatter-gather: no tenant → ask EVERY shard in parallel and merge. N queries, as slow as the
    //    slowest shard, fails if any shard is down. Keep these rare (admin reports), or use a
    //    reporting copy (data warehouse) instead.
    public async Task<int> CountAllOrdersAsync()
    {
        var perShard = shards.AllShardConnectionStrings.Select(async cs =>
        {
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT COUNT(*) FROM Orders", conn);
            return (int)(await cmd.ExecuteScalarAsync())!;
        });
        return (await Task.WhenAll(perShard)).Sum();
    }
}

// Other strategies, for comparison (no directory needed, but less control):
//   Range: tenantId <= 1000 → shard-1, <= 2000 → shard-2 ...  New tenants all land on the LAST shard (hot).
//   Hash:  shards[(uint)tenantId.GetHashCode() % shards.Count]  Even spread, but adding a shard changes N
//          and moves most tenants (consistent hashing fixes that, M17). Can't move ONE tenant by choice.
//   Directory (above): one lookup (cached), but full control: move any tenant, give a big tenant its own shard.
