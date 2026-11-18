using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace M11.Messaging;

// Outbox table:
//   CREATE TABLE Outbox (
//     Seq       BIGINT IDENTITY PRIMARY KEY,   -- publish order
//     MessageId UNIQUEIDENTIFIER NOT NULL,     -- consumer dedups on this (inbox)
//     Type      NVARCHAR(200) NOT NULL,        -- "OrderPlaced"
//     Key       NVARCHAR(100) NOT NULL,        -- aggregate id → Kafka key / ordering
//     Payload   NVARCHAR(MAX) NOT NULL,
//     SentAt    DATETIME2 NULL);               -- NULL = not published yet
//   CREATE INDEX IX_Outbox_Unsent ON Outbox(Seq) WHERE SentAt IS NULL;   -- filtered index (M6)

public interface IMessagePublisher { Task PublishAsync(Guid messageId, string type, string key, string payload); }

// ❌ BEFORE: dual write. Commit, then publish: a crash between them loses the event forever.
public class OrderServiceBefore(string cs, IMessagePublisher bus)
{
    public async Task PlaceOrderAsync(Guid orderId, string sku)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await new SqlCommand("INSERT Orders(Id, Sku) VALUES (@id, @sku)", conn)
            { Parameters = { new("@id", orderId), new("@sku", sku) } }.ExecuteNonQueryAsync();
        await bus.PublishAsync(Guid.NewGuid(), "OrderPlaced", orderId.ToString(), sku);   // 💥 here = order without event
    }
}

// ✅ AFTER (1/2): order + event row in ONE transaction. No broker call in the request path.
public class OrderService(string cs)
{
    public async Task PlaceOrderAsync(Guid orderId, string sku)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        await new SqlCommand("INSERT Orders(Id, Sku) VALUES (@id, @sku)", conn, tx)
            { Parameters = { new("@id", orderId), new("@sku", sku) } }.ExecuteNonQueryAsync();

        await new SqlCommand("INSERT Outbox(MessageId, Type, [Key], Payload) VALUES (@mid, 'OrderPlaced', @key, @p)", conn, tx)
            { Parameters = { new("@mid", Guid.NewGuid()), new("@key", orderId.ToString()),
                             new("@p", JsonSerializer.Serialize(new { orderId, sku })) } }.ExecuteNonQueryAsync();

        await tx.CommitAsync();   // both rows or neither: no lost event, no ghost event
    }
}

// ✅ AFTER (2/2): relay publishes unsent rows. Safe to run several instances.
public class OutboxRelay(string cs, IMessagePublisher bus) : BackgroundService
{
    // UPDLOCK: claim the rows so no other relay takes them.  READPAST: skip rows another relay claimed.
    // (READ UNCOMMITTED would be wrong: no claim, AND it could publish rows from a tx that rolls back.)
    private const string Claim = """
        SELECT TOP (100) Seq, MessageId, Type, [Key], Payload
        FROM Outbox WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE SentAt IS NULL ORDER BY Seq
        """;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { if (await PublishBatchAsync() == 0) await Task.Delay(500, ct); }   // idle → back off
            catch (Exception) when (!ct.IsCancellationRequested) { await Task.Delay(2000, ct); } // broker/DB down → rows stay unsent
        }
    }

    private async Task<int> PublishBatchAsync()
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();          // locks from Claim live until Commit

        var rows = new List<(long Seq, Guid Id, string Type, string Key, string Payload)>();
        await using (var r = await new SqlCommand(Claim, conn, tx).ExecuteReaderAsync())
            while (await r.ReadAsync()) rows.Add((r.GetInt64(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetString(4)));

        foreach (var m in rows)
        {
            await bus.PublishAsync(m.Id, m.Type, m.Key, m.Payload);   // a crash after this line → row re-sent later
            await new SqlCommand("UPDATE Outbox SET SentAt = SYSUTCDATETIME() WHERE Seq = @s", conn, tx)
                { Parameters = { new("@s", m.Seq) } }.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return rows.Count;
    }
}
// Guarantees: never lost, never a ghost, but AT LEAST ONCE (crash between publish and commit → duplicate),
//             so consumers dedup on MessageId (inbox, next session).
// Trade-off:  several relays + READPAST = no GLOBAL order. Need per-order order? One active relay
//             (sp_getapplock) or partition relays by hash(Key).
// Cleanup:    a job deletes rows WHERE SentAt < now - 7 days, in batches.
