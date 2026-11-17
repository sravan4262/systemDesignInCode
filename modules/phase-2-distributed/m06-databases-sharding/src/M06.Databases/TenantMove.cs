using Microsoft.Data.SqlClient;

namespace M06.Databases;

// Rebalancing: move ONE tenant from shard A to shard B while the app keeps running.
//   ① COPY → ② CATCH UP → ③ FREEZE (fence old shard) + final catch-up + VERIFY → ④ SWITCH directory → ⑤ CLEAN UP later
// Builds on ShardRouting.cs (ShardResolver, ShardMapOptions).

// ---------- The directory, now with status + version (optimistic concurrency) ----------
public enum TenantStatus { Active, Moving }
public record TenantPlacement(string ShardId, TenantStatus Status, long Version);

public interface ITenantMoveDirectory
{
    Task<TenantPlacement> GetAsync(int tenantId);

    // Compare-and-swap: only succeeds if nobody changed the row since we read it (Version matches).
    //   UPDATE TenantShards SET Status=@s, Version=Version+1 WHERE TenantId=@t AND Version=@v
    Task<bool> TrySetStatusAsync(int tenantId, TenantStatus status, long expectedVersion);

    //   UPDATE TenantShards SET ShardId=@new, Status='Active', Version=Version+1 WHERE TenantId=@t AND Version=@v
    Task<bool> TrySwitchShardAsync(int tenantId, string newShardId, long expectedVersion);
}

// ---------- The data side: what runs against the shards ----------
public interface ITenantDataMover
{
    // ① Bulk copy all tenant rows from → to. Returns a watermark (e.g. max rowversion copied).
    //    Idempotent: MERGE/upsert by primary key, so a retried copy doesn't duplicate rows.
    Task<long> CopyAsync(int tenantId, string from, string to);

    // ② Copy rows changed after the watermark (rowversion > @w; deletes via a tombstone/change table).
    Task<(long Watermark, int RowsApplied)> CatchUpAsync(int tenantId, string from, string to, long sinceWatermark);

    // ③ Row counts + checksums per table match on both shards?
    Task<bool> VerifyAsync(int tenantId, string from, string to);

    // ③ Fence: from now on the OLD shard rejects writes for this tenant. Every write proc starts with:
    //    IF EXISTS (SELECT 1 FROM TenantFences WHERE TenantId=@t) THROW 50001, 'Tenant fenced', 1;
    Task FenceAsync(int tenantId, string shard);
    Task UnfenceAsync(int tenantId, string shard);

    // ⑤ Delete the tenant's rows from the old shard (after a safety period; keeps rollback easy until then).
    Task DeleteTenantAsync(int tenantId, string shard);
}

public record MoveResult(bool Success, string Message);

// ---------- The orchestrator ----------
public class TenantMover(ITenantMoveDirectory directory, ITenantDataMover data)
{
    private const int CatchUpRounds = 5;       // stop chasing after N rounds...
    private const int SmallEnoughGap = 100;    // ...or once a round changes fewer rows than this

    public async Task<MoveResult> MoveAsync(int tenantId, string toShard)
    {
        var placement = await directory.GetAsync(tenantId);
        if (placement.ShardId == toShard) return new(true, "Already on target shard.");
        if (placement.Status != TenantStatus.Active) return new(false, "Another move is in progress.");
        var from = placement.ShardId;

        // ① COPY (hours; app keeps writing to `from` normally)
        var watermark = await data.CopyAsync(tenantId, from, toShard);

        // ② CATCH UP until the remaining gap is small
        for (var round = 0; round < CatchUpRounds; round++)
        {
            (watermark, var applied) = await data.CatchUpAsync(tenantId, from, toShard, watermark);
            if (applied < SmallEnoughGap) break;
        }

        // ③ FREEZE: mark Moving (CAS stops two movers racing), then fence the old shard so NO server
        //    can write there — including servers with a stale cached "42 → from".
        if (!await directory.TrySetStatusAsync(tenantId, TenantStatus.Moving, placement.Version))
            return new(false, "Directory changed during the move; aborted before freezing.");
        var version = placement.Version + 1;

        await data.FenceAsync(tenantId, from);
        try
        {
            (watermark, _) = await data.CatchUpAsync(tenantId, from, toShard, watermark);   // last few rows

            if (!await data.VerifyAsync(tenantId, from, toShard))
            {
                await RollBackFreezeAsync(tenantId, from, version);
                return new(false, "Verification failed; tenant stays on " + from);
            }

            // ④ SWITCH: one atomic directory update. From now on every fresh lookup says `toShard`.
            if (!await directory.TrySwitchShardAsync(tenantId, toShard, version))
                throw new InvalidOperationException("Directory changed while frozen.");
        }
        catch
        {
            await RollBackFreezeAsync(tenantId, from, version);
            throw;
        }

        // ⑤ The fence on `from` STAYS until cleanup: stale servers keep getting rejected there,
        //    invalidate their cache, and retry against `toShard`.
        return new(true, $"Moved to {toShard}. Delete tenant data from {from} after the safety period.");
    }

    // Run later (e.g. a day after), once you're sure nothing reads/writes the old copy.
    public Task CleanUpAsync(int tenantId, string oldShard) => data.DeleteTenantAsync(tenantId, oldShard);

    private async Task RollBackFreezeAsync(int tenantId, string from, long version)
    {
        await data.UnfenceAsync(tenantId, from);                                   // writes allowed again
        await directory.TrySetStatusAsync(tenantId, TenantStatus.Active, version); // still on `from`
    }
}

// ---------- The write path: how app servers survive the move ----------
public class TenantMovedOrFrozenException(int tenantId)
    : Exception($"Tenant {tenantId} is being moved. Retry shortly.");   // → HTTP 503 + Retry-After

public class ShardAwareWriter(ShardResolver resolver, ShardMapOptions shards)
{
    private const int FencedErrorNumber = 50001;

    public async Task ExecuteWriteAsync(int tenantId, Func<SqlConnection, Task> write)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var shardId = await resolver.ResolveAsync(tenantId);    // may be a stale cached answer
            await using var conn = new SqlConnection(shards.Shards[shardId]);
            await conn.OpenAsync();
            try
            {
                await write(conn);
                return;
            }
            catch (SqlException ex) when (ex.Number == FencedErrorNumber)
            {
                // Old shard said "not here anymore" (or "frozen"). Drop the cached answer and look again:
                //   after the switch → fresh lookup returns the NEW shard → retry succeeds;
                //   during the freeze → directory still says old shard → fenced again → 503, client retries.
                resolver.Invalidate(tenantId);
                if (attempt == 2) throw new TenantMovedOrFrozenException(tenantId);
            }
        }
    }
}
