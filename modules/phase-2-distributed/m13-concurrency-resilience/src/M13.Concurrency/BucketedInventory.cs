using Microsoft.Data.SqlClient;

namespace M13.Concurrency;

// Hot-row fix: split ONE inventory row into N bucket rows so requests spread out.
//
//   ❌ BEFORE: Inventory(EventId PK, Available)            → 1 row,  20,000 requests queue on it
//   ✅ AFTER:  Inventory(EventId, Bucket, Available,        → 10 rows, ~2,000 requests per row
//                        PRIMARY KEY (EventId, Bucket))
//
//   Seed 100 seats as 10 × 10:
//   INSERT Inventory(EventId, Bucket, Available)
//   SELECT @e, b, 10 FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) AS t(b);

public class BucketedInventory(string cs, int bucketCount = 10)
{
    // Same atomic conditional UPDATE as before, just aimed at ONE bucket row.
    private const string ClaimSql = """
        UPDATE Inventory SET Available = Available - 1
        WHERE EventId = @e AND Bucket = @b AND Available >= 1
        """;

    public async Task<bool> TryClaimAsync(Guid eventId)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        var start = Random.Shared.Next(bucketCount);           // random start → load spreads across rows
        for (var i = 0; i < bucketCount; i++)
        {
            var bucket = (start + i) % bucketCount;            // wrap around: 7, 8, 9, 0, 1, …
            var rows = await new SqlCommand(ClaimSql, conn)
                { Parameters = { new("@e", eventId), new("@b", bucket) } }.ExecuteNonQueryAsync();

            if (rows == 1) return true;                        // got a seat from this bucket
            // rows == 0 → this bucket is empty, try the next one
        }
        return false;                                          // every bucket empty → sold out
    }

    // Cancel / hold expired: give the seat back to any bucket (totals are what matter).
    public async Task ReleaseAsync(Guid eventId)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await new SqlCommand("UPDATE Inventory SET Available = Available + 1 WHERE EventId = @e AND Bucket = @b", conn)
            { Parameters = { new("@e", eventId), new("@b", Random.Shared.Next(bucketCount)) } }.ExecuteNonQueryAsync();
    }

    // "Seats left" for the UI = sum of buckets (read can be slightly stale; that's fine for display).
    public async Task<int> AvailableAsync(Guid eventId)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        return (int)(await new SqlCommand("SELECT ISNULL(SUM(Available), 0) FROM Inventory WHERE EventId = @e", conn)
            { Parameters = { new("@e", eventId) } }.ExecuteScalarAsync())!;
    }
}
// Trade-offs:
//  - Near sell-out most buckets are 0, so a request may try several before finding a seat
//    (more round-trips exactly when traffic peaks). The last few seats are contended again.
//  - Single-statement alternative: let SQL pick any unlocked, non-empty bucket and SKIP locked ones:
//      UPDATE TOP (1) Inventory WITH (UPDLOCK, READPAST, ROWLOCK)
//      SET Available = Available - 1 OUTPUT inserted.Bucket
//      WHERE EventId = @e AND Available >= 1;
//    (0 rows can also mean "all non-empty buckets locked right now" → retry once before saying sold out.)
//  - Only worth it for a genuinely hot row. Normal traffic: one row + atomic UPDATE is enough.
