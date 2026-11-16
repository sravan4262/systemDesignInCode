using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace M02.Checkout.DesignPatterns;

// Singleton: exactly one instance for the whole app, shared by everyone.
public static class Singleton
{
    // ❌ BEFORE
    // Problem: a hand-rolled "naive" singleton.
    //   - Not thread-safe: two threads see _instance == null at the same time → two instances.
    //   - Dictionary isn't thread-safe either; concurrent requests can corrupt it.
    //   - Callers reach for ExchangeRateCacheBefore.Instance directly: a hidden global dependency
    //     you can't see in the constructor and can't mock in tests.
    public class ExchangeRateCacheBefore
    { 
        private static ExchangeRateCacheBefore? _instance;
        private readonly Dictionary<string, decimal> _rates = [];

        public static ExchangeRateCacheBefore Instance => _instance ??= new ExchangeRateCacheBefore();

        public decimal GetRate(string currency)
        {
            if (!_rates.TryGetValue(currency, out var rate))
                _rates[currency] = rate = 1.1m;   // pretend: call the external rates API
            return rate;
        }
    }

    public class PriceServiceBefore
    {
        // Hidden dependency: nothing in the constructor tells you this class needs the cache.
        public decimal Convert(decimal amount, string currency) =>
            amount * ExchangeRateCacheBefore.Instance.GetRate(currency);
    }

    // ✅ AFTER (option A): classic singleton, done right.
    // How this solves it: Lazy<T> guarantees ONE instance even under concurrent first access,
    // and ConcurrentDictionary makes the shared state safe. Still a global though (hard to mock).
    public sealed class ExchangeRateCacheClassic
    {
        private static readonly Lazy<ExchangeRateCacheClassic> _instance = new(() => new());
        private readonly ConcurrentDictionary<string, decimal> _rates = new();

        private ExchangeRateCacheClassic() { }   // nobody else can call `new`

        public static ExchangeRateCacheClassic Instance => _instance.Value;

        public decimal GetRate(string currency) => _rates.GetOrAdd(currency, _ => 1.1m);
    }

    // ✅ AFTER (option B, preferred in .NET): a normal class; DI guarantees one instance.
    // How this solves it: no static Instance, no private constructor. "Only one" is a
    // registration choice (AddSingleton), the dependency is visible, and tests can mock it.
    public interface IExchangeRateCache
    {
        decimal GetRate(string currency);
    }

    public class ExchangeRateCache : IExchangeRateCache
    {
        // Shared by every request → must be thread-safe.
        private readonly ConcurrentDictionary<string, decimal> _rates = new();

        public decimal GetRate(string currency) => _rates.GetOrAdd(currency, _ => 1.1m);
    }

    // The dependency is explicit in the constructor.
    public class PriceService(IExchangeRateCache cache)
    {
        public decimal Convert(decimal amount, string currency) => amount * cache.GetRate(currency);
    }

    // ===== What goes in Program.cs =====
    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ONE cache for the whole app.
            builder.Services.AddSingleton<IExchangeRateCache, ExchangeRateCache>();

            // PriceService is stateless, so Singleton would also be fine. Scoped shown here on purpose:
            // a scoped service MAY depend on a singleton. The reverse (singleton → scoped, e.g. DbContext)
            // is a "captive dependency" bug: the scoped object gets trapped for the app's whole life.
            builder.Services.AddScoped<PriceService>();

            var app = builder.Build();

            // GET /price?amount=100&currency=EUR
            app.MapGet("/price", (decimal amount, string currency, PriceService prices) =>
                Results.Ok(prices.Convert(amount, currency)));

            app.Run();
        }
    }
}
