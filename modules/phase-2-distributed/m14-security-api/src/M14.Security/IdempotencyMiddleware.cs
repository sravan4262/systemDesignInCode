using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace M14.Security;

// Storage (SQL):
//   CREATE TABLE IdempotencyKeys (
//     Scope        NVARCHAR(200) PRIMARY KEY,   -- "<user oid>:<Idempotency-Key>" → users can't collide
//     RequestHash  BINARY(32) NOT NULL,         -- detects the same key reused for a different body
//     StatusCode   INT NULL,                    -- NULL = still processing
//     ContentType  NVARCHAR(100) NULL,
//     Body         VARBINARY(MAX) NULL,
//     CreatedAt    DATETIME2 NOT NULL);         -- cleanup job deletes rows older than 24 h
public record IdempotencyRecord(byte[] RequestHash, int? StatusCode, string? ContentType, byte[]? Body);

public interface IIdempotencyStore
{
    // Atomic claim: INSERT the row (StatusCode NULL). Returns null if WE claimed it, else the existing row.
    // The PRIMARY KEY is the referee, same as the inbox: no check-then-act race between two retries.
    Task<IdempotencyRecord?> TryBeginAsync(string scope, byte[] requestHash);
    Task CompleteAsync(string scope, int statusCode, string? contentType, byte[] body);
    Task ReleaseAsync(string scope);   // DELETE the row so a retry can run again
}

// ❌ BEFORE: no key. Client times out, retries POST /payments → a second charge.
// ✅ AFTER: same key → same stored response, the handler runs at most once per key.
public class IdempotencyMiddleware(RequestDelegate next, IIdempotencyStore store)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!HttpMethods.IsPost(ctx.Request.Method) ||
            ctx.Request.Headers["Idempotency-Key"].FirstOrDefault() is not { Length: > 0 } key)
        {
            await next(ctx);
            return;
        }

        var scope = $"{ctx.User.FindFirstValue("oid")}:{key}";
        ctx.Request.EnableBuffering();                                        // read body, then rewind for the handler
        var hash = await SHA256.HashDataAsync(ctx.Request.Body);
        ctx.Request.Body.Position = 0;

        var existing = await store.TryBeginAsync(scope, hash);
        if (existing is not null)
        {
            if (!existing.RequestHash.AsSpan().SequenceEqual(hash)) { ctx.Response.StatusCode = 422; return; } // key reused, other body
            if (existing.StatusCode is null) { ctx.Response.StatusCode = 409; return; }                      // first attempt still running
            ctx.Response.StatusCode = existing.StatusCode.Value;                                              // REPLAY
            ctx.Response.ContentType = existing.ContentType;
            await ctx.Response.Body.WriteAsync(existing.Body);
            return;
        }

        // We own the key: run the real handler, capturing what it writes.
        var original = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try
        {
            await next(ctx);
        }
        catch
        {
            await store.ReleaseAsync(scope);                                  // crashed → let a retry try again
            throw;
        }
        finally { ctx.Response.Body = original; }

        if (ctx.Response.StatusCode >= 500) await store.ReleaseAsync(scope);  // server error → don't pin it
        else await store.CompleteAsync(scope, ctx.Response.StatusCode, ctx.Response.ContentType, buffer.ToArray());

        buffer.Position = 0;
        await buffer.CopyToAsync(original);
    }
}
// Program.cs:  builder.Services.AddSingleton<IIdempotencyStore, SqlIdempotencyStore>();
//              app.UseAuthentication(); app.UseAuthorization();
//              app.UseMiddleware<IdempotencyMiddleware>();          // after auth: scope uses the caller's oid
// Strongest version: write the business change and CompleteAsync in the SAME DB transaction,
// so "charged but response not stored" can't happen (same idea as outbox/inbox).
// External side effects (Stripe) still pass their own idempotency key (e.g. the same key) downstream.
