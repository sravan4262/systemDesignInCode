using Microsoft.Data.SqlClient;

namespace M11.Messaging;

// Inbox table:
//   CREATE TABLE Inbox (
//     MessageId   UNIQUEIDENTIFIER NOT NULL,
//     Consumer    NVARCHAR(100)    NOT NULL,    -- several handlers may process the same message
//     ProcessedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
//     CONSTRAINT PK_Inbox PRIMARY KEY (MessageId, Consumer));   -- ← the dedup referee

public record OrderPlaced(Guid MessageId, Guid OrderId, decimal Amount);
public interface IPaymentGateway { Task<string> ChargeAsync(string idempotencyKey, decimal amount); }   // e.g. Stripe

// ❌ BEFORE: check-then-act. Two instances both see "not processed" → both charge.
public class BillingHandlerBefore(string cs, IPaymentGateway stripe)
{
    public async Task HandleAsync(OrderPlaced msg)
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        var seen = await new SqlCommand("SELECT COUNT(*) FROM Inbox WHERE MessageId = @m", conn)
            { Parameters = { new("@m", msg.MessageId) } }.ExecuteScalarAsync();
        if ((int)seen! > 0) return;
        // ⚠️ instance B can pass the same check right here
        await stripe.ChargeAsync(Guid.NewGuid().ToString(), msg.Amount);   // ⚠️ new key each try → Stripe can't dedup
        // ... insert Payments, insert Inbox (separate statements, no transaction)
    }
}

// ✅ AFTER: the PK decides, inside the SAME transaction as the business change.
public class BillingHandler(string cs, IPaymentGateway stripe)
{
    public async Task HandleAsync(OrderPlaced msg)   // caller ACKs when this returns; throws → redelivery
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        try
        {
            // 1) Claim the message. A concurrent duplicate BLOCKS here until we commit, then hits the PK.
            await new SqlCommand("INSERT Inbox(MessageId, Consumer) VALUES (@m, 'Billing')", conn, tx)
                { Parameters = { new("@m", msg.MessageId) } }.ExecuteNonQueryAsync();
        }
        catch (SqlException e) when (e.Number is 2627 or 2601)   // PK / unique violation
        {
            return;                                               // already processed → just ACK
        }

        // 2) External side effect: can't join our tx, so make it idempotent with a STABLE key.
        //    Redelivery after a failed commit → same key → Stripe returns the original charge, no new charge.
        var chargeId = await stripe.ChargeAsync($"order-{msg.OrderId}", msg.Amount);

        // 3) Business change, same tx as the inbox row.
        await new SqlCommand("INSERT Payments(OrderId, ChargeId, Amount) VALUES (@o, @c, @a)", conn, tx)
            { Parameters = { new("@o", msg.OrderId), new("@c", chargeId), new("@a", msg.Amount) } }.ExecuteNonQueryAsync();

        await tx.CommitAsync();   // inbox + payment, both or neither
    }
}
// Failure walk-through:
//   crash before commit  → tx rolls back (inbox row gone) → redelivery processes again → Stripe dedups by key ✅
//   crash after commit   → redelivery → INSERT Inbox → 2627 → return → ACK ✅
//   two instances at once→ 2nd blocks on the PK lock, then 2627 → return ✅
// Trade-off: the tx (and the inbox row lock) stays open during the HTTP call. Keep a short timeout.
// Cleanup:  DELETE Inbox WHERE ProcessedAt < now - 30 days (longer than any redelivery/replay window).
