using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace M02.Checkout.DesignPatterns;

// Strategy: interchangeable algorithms behind one interface, picked at runtime.
public static class Strategy
{
    public record Cart(decimal Total);

    // ❌ BEFORE
    // Problem: every discount rule lives inside Checkout's switch.
    //   - A new discount (BOGO, TIERED, ...) means editing Checkout again (breaks Open/Closed).
    //   - Rules can't be unit-tested on their own; you have to go through Checkout.
    //   - The method keeps growing, and two devs adding discounts collide in the same switch.
    public class CheckoutBefore
    {
        public decimal ApplyDiscount(Cart cart, string code)
        {
            switch (code)
            {
                case "PERCENT10":
                    return cart.Total * 0.90m;

                case "FLAT20":
                    return Math.Max(0, cart.Total - 20);

                default:
                    return cart.Total;
            }
        }
    }

    // ✅ AFTER

    // The strategy interface: every discount looks the same to Checkout.
    // How this solves it: Checkout depends only on IDiscount, never on a concrete rule.
    public interface IDiscount
    {
        string Code { get; }              // which code this rule handles (used as the dictionary key)
        decimal Apply(Cart cart);         // the rule itself; no `code` param, selection is Checkout's job
    }

    // Strategy #1: percentage off.
    // How this solves it: the PERCENT10 rule lives in its own class, testable on its own.
    // The percent is a constructor arg, so one class covers PERCENT10, PERCENT25, etc.
    public class PercentOff(string code, decimal percent) : IDiscount
    {
        public string Code => code;
        public decimal Apply(Cart cart) => cart.Total * (1 - percent / 100);
    }

    // Strategy #2: flat amount off.
    // How this solves it: the FLAT20 rule (including "never below 0") is isolated here.
    public class FlatOff(string code, decimal amount) : IDiscount
    {
        public string Code => code;
        public decimal Apply(Cart cart) => Math.Max(0, cart.Total - amount);
    }

    // Context: picks the right strategy by code at runtime.
    // How this solves it: the discounts are handed in from outside (DI) and put in a dictionary.
    // A new discount = a new IDiscount class + one registration. This class never changes.
    public class CheckoutAfter
    {
        private readonly Dictionary<string, IDiscount> _discounts;

        public CheckoutAfter(IEnumerable<IDiscount> discounts)
        {
            // ToDictionary throws on duplicate codes, so a bad registration fails fast at startup.
            _discounts = discounts.ToDictionary(d => d.Code);
        }

        public decimal ApplyDiscount(Cart cart, string code) =>
            _discounts.TryGetValue(code, out var discount)
                ? discount.Apply(cart)
                : cart.Total; // unknown code: no discount (same behavior as Before)
    }

    // ===== What goes in Program.cs (the composition root) =====
    // This is the ONLY place that knows the full list of discounts and CREATES them.
    // CheckoutAfter never calls `new` on a discount; it only SELECTS and CALLS.
    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Register every strategy under the SAME interface.
            //    Singleton: they're stateless (fixed config only), so one shared instance is safe.
            //    Adding BOGO later = a new class + one line here. Nothing else changes.
            builder.Services.AddSingleton<IDiscount>(new PercentOff("PERCENT10", 10));
            builder.Services.AddSingleton<IDiscount>(new FlatOff("FLAT20", 20));

            // 2. Register the context. DI sees it needs IEnumerable<IDiscount>
            //    and injects ALL the IDiscount registrations above.
            //    Singleton too: its only dependencies are singletons, and the dictionary is read-only,
            //    so it's built once instead of on every request.
            builder.Services.AddSingleton<CheckoutAfter>();

            var app = builder.Build();

            // 3. Endpoint: the HTTP layer asks DI for CheckoutAfter and passes the request data in.
            //    GET /checkout/discount?total=100&code=FLAT20  → 80
            app.MapGet("/checkout/discount", (decimal total, string code, CheckoutAfter checkout) =>
                Results.Ok(new { total, code, final = checkout.ApplyDiscount(new Cart(total), code) }));

            app.Run();
        }
    }
}
