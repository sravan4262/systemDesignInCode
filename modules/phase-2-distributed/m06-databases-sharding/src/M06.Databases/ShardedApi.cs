using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace M06.Databases;

// API level: how a request becomes a connection to the right server.
//   Request → ⓐ which tenant? (token) → ① which shard? (ShardResolver) → ② which server? (primary/replica)
// Uses ShardResolver / IShardDirectory from ShardRouting.cs and DbIntent from ReadWriteRouting.cs.

// ⓐ WHICH TENANT: from the logged-in user's token, never from the URL or body (a user could fake those).
public interface ITenantContext
{
    int TenantId { get; }
}

public class ClaimsTenantContext(IHttpContextAccessor http) : ITenantContext
{
    public int TenantId =>
        int.TryParse(http.HttpContext?.User.FindFirstValue("tenant_id"), out var id)
            ? id
            : throw new UnauthorizedAccessException("No tenant_id claim on this request.");
}

// Replicas per shard (primaries come from ShardMapOptions.Shards).
public record ShardReplicaOptions(IReadOnlyDictionary<string, IReadOnlyList<string>> Replicas);

// ① + ② in ONE place: repositories ask for "a connection for reading/writing"; this decides the server.
public class TenantDbConnectionFactory(
    ITenantContext tenant,
    ShardResolver resolver,
    ShardMapOptions shards,
    ShardReplicaOptions replicas)
{
    private static int _next = -1;

    public async Task<SqlConnection> OpenAsync(DbIntent intent)
    {
        // ① Which shard? tenant → shard (cached directory lookup).
        var shardId = await resolver.ResolveAsync(tenant.TenantId);

        // ② Which server inside that shard? write → primary, read → a replica (round-robin).
        //    (Add read-your-writes from ReadWriteRouting.cs here if users must see their own writes.)
        var connectionString = intent == DbIntent.Write
            ? shards.Shards[shardId]
            : PickReplica(shardId) ?? shards.Shards[shardId];          // no replicas → primary

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private string? PickReplica(string shardId)
    {
        if (!replicas.Replicas.TryGetValue(shardId, out var list) || list.Count == 0) return null;
        var i = (int)((uint)Interlocked.Increment(ref _next) % (uint)list.Count);
        return list[i];
    }
}

// Repository: knows SQL, not servers. Still filters by TenantId (a shard holds many tenants).
public class TenantOrdersRepository(TenantDbConnectionFactory db, ITenantContext tenant)
{
    public async Task<int> CountRecentAsync()
    {
        await using var conn = await db.OpenAsync(DbIntent.Read);                       // → shard replica
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM Orders WHERE TenantId = @t AND CreatedAt >= DATEADD(day, -7, SYSUTCDATETIME())", conn);
        cmd.Parameters.AddWithValue("@t", tenant.TenantId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<int> CreateAsync(decimal total)
    {
        await using var conn = await db.OpenAsync(DbIntent.Write);                      // → shard primary
        await using var cmd = new SqlCommand(
            "INSERT INTO Orders (TenantId, Total, CreatedAt) OUTPUT INSERTED.Id VALUES (@t, @total, SYSUTCDATETIME())", conn);
        cmd.Parameters.AddWithValue("@t", tenant.TenantId);
        cmd.Parameters.AddWithValue("@total", total);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }
}

// ===== What goes in Program.cs =====
public static class ShardedApiProgram
{
    public static void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Config (appsettings / App Configuration; no passwords: use managed identity):
        //   "Shards":   { "shard-1": "Server=sql-shard-1;...", "shard-2": "Server=sql-shard-2;..." }
        //   "Replicas": { "shard-1": ["Server=sql-shard-1-r1;..."], "shard-2": ["Server=sql-shard-2-r1;..."] }
        var shards = new ShardMapOptions(
            builder.Configuration.GetSection("Shards").Get<Dictionary<string, string>>() ?? [],
            DirectoryCacheTtl: TimeSpan.FromMinutes(1));
        var replicas = new ShardReplicaOptions(
            builder.Configuration.GetSection("Replicas").Get<Dictionary<string, IReadOnlyList<string>>>() ?? []);

        // Singletons: shared, thread-safe, hold the cache.
        builder.Services.AddSingleton(shards);
        builder.Services.AddSingleton(replicas);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IShardDirectory, InMemoryShardDirectory>();   // prod: SQL catalog DB
        builder.Services.AddSingleton<ShardResolver>();

        // Scoped: per request (they depend on the current user's tenant).
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();
        builder.Services.AddScoped<TenantDbConnectionFactory>();
        builder.Services.AddScoped<TenantOrdersRepository>();

        builder.Services.AddAuthentication();   // + JWT bearer setup issuing the tenant_id claim
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        // The endpoint knows NOTHING about shards: tenant from the token, server picked by the factory.
        app.MapGet("/orders/recent/count", async (TenantOrdersRepository orders) =>
            Results.Ok(await orders.CountRecentAsync())).RequireAuthorization();

        app.MapPost("/orders", async (decimal total, TenantOrdersRepository orders) =>
            Results.Ok(await orders.CreateAsync(total))).RequireAuthorization();

        app.Run();
    }
}
