using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace M02.Checkout.DesignPatterns;

// Chain of Responsibility: pass a request along a chain; each handler either STOPS it or passes it ON.
public static class ChainOfResponsibility
{
    public record CheckoutRequest(int Quantity, string Country, decimal Total);

    // ❌ BEFORE
    // Problem: every check lives in one method.
    //   - Grows with each new check; can't reorder, reuse, or switch one off per region.
    //   - Checks can't be tested alone.
    //   - "Run the slow fraud API only if cheap checks pass" is hidden in the if-order.
    public class CheckoutValidatorBefore
    {
        public string? Validate(CheckoutRequest r)
        {
            if (r.Quantity > 10)                return "Out of stock";
            if (r.Country is not ("US" or "CA")) return "We can't ship there";
            if (r.Total > 5000)                 return "Flagged for fraud review";
            return null;   // null = all good
        }
    }

    // ✅ AFTER

    // The link: each handler knows only the NEXT one.
    // Returns an error to STOP the chain, or calls Next to pass it on. End of chain = null (all good).
    public abstract class CheckoutCheck
    {
        private CheckoutCheck? _next;

        // Returns `next` so links can be chained: stock.SetNext(address).SetNext(fraud)
        public CheckoutCheck SetNext(CheckoutCheck next) { _next = next; return next; }

        public abstract string? Check(CheckoutRequest r);

        protected string? Next(CheckoutRequest r) => _next?.Check(r);
    }

    // How this solves it: each check is its own small, testable class.
    public class StockCheck : CheckoutCheck
    {
        public override string? Check(CheckoutRequest r) =>
            r.Quantity > 10 ? "Out of stock" : Next(r);                 // stop, or pass on
    }

    public class AddressCheck : CheckoutCheck
    {
        public override string? Check(CheckoutRequest r) =>
            r.Country is "US" or "CA" ? Next(r) : "We can't ship there";
    }

    // Slow/expensive (pretend: external fraud API). Placed LAST so it only runs if the cheap checks passed.
    public class FraudCheck : CheckoutCheck
    {
        public override string? Check(CheckoutRequest r) =>
            r.Total > 5000 ? "Flagged for fraud review" : Next(r);
    }

    // ===== What goes in Program.cs =====
    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Build the chain ONCE: the order is decided here. Add/remove/reorder a check = edit these lines only.
            // Singleton is safe: the links hold no per-request state.
            builder.Services.AddSingleton<CheckoutCheck>(_ =>
            {
                var head = new StockCheck();
                head.SetNext(new AddressCheck())
                    .SetNext(new FraudCheck());
                return head;   // callers only get the FIRST link
            });

            var app = builder.Build();

            // POST /checkout/validate?quantity=2&country=US&total=100
            app.MapPost("/checkout/validate", (int quantity, string country, decimal total, CheckoutCheck chain) =>
                chain.Check(new CheckoutRequest(quantity, country, total)) is { } error
                    ? Results.BadRequest(error)
                    : Results.Ok("Checkout can proceed"));

            app.Run();
        }
    }
}

// For comparison:
//   List style (very common in .NET): inject IEnumerable<ICheck> and loop; first error wins.
//     foreach (var c in checks) if (c.Check(r) is { } error) return error;
//   ASP.NET Core middleware = CoR with a `next` delegate; not calling next() short-circuits (e.g. 401/403).
//   Key trait: a CoR link MAY stop the request; later links never see it.
