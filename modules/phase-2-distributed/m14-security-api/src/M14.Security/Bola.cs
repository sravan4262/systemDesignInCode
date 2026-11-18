using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace M14.Security;

public record Order(int Id, string OwnerOid, string Address);

public static class Bola
{
    private static readonly List<Order> Orders = [new(1001, "alice-oid", "1 Main St"), new(1002, "bob-oid", "9 Elm Rd")];

    // The caller's identity comes from the VALIDATED token (Entra "oid" claim), never from the request body/URL.
    private static string? CallerOid(ClaimsPrincipal user) =>
        user.FindFirstValue("oid") ?? user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

    public static void Map(WebApplication app)
    {
        // ❌ BEFORE: authenticated, but no ownership check. Alice can read /orders/1002 (Bob's).
        app.MapGet("/v0/orders/{id:int}", (int id) =>
                Orders.FirstOrDefault(o => o.Id == id) is { } o ? Results.Ok(o) : Results.NotFound())
           .RequireAuthorization();

        // ✅ AFTER: the ownership check is part of the lookup itself (in SQL: WHERE Id = @id AND OwnerOid = @oid).
        app.MapGet("/orders/{id:int}", (int id, ClaimsPrincipal user) =>
            {
                var oid = CallerOid(user);
                var order = Orders.FirstOrDefault(o => o.Id == id && o.OwnerOid == oid);
                return order is null ? Results.NotFound() : Results.Ok(order);   // 404, not 403: don't confirm 1002 exists
            })
           .RequireAuthorization();

        // ✅ Role-based exception: support staff may read any order (role from the token's "roles" claim).
        app.MapGet("/admin/orders/{id:int}", (int id) =>
                Orders.FirstOrDefault(o => o.Id == id) is { } o ? Results.Ok(o) : Results.NotFound())
           .RequireAuthorization(p => p.RequireRole("Orders.Support"));
    }
}
// Systematic prevention (so nobody forgets on endpoint #47):
//  - Scope every query by the caller: a repository method GetForUser(id, oid), or an EF Core global
//    query filter on OwnerId / TenantId, so the unsafe query isn't even easy to write.
//  - Take the user id ONLY from the token, never from a route/body field like ?userId=.
//  - Prefer non-guessable ids (GUIDs) as defense in depth: it slows enumeration but is NOT the fix.
//  - Test it: "user A requests user B's resource → 404" for every endpoint that takes an id.
