using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace M02.Checkout.DesignPatterns;

// Adapter: a wrapper that makes a class with the WRONG interface fit the interface our code expects.
public static class Adapter
{
    // --- Third-party SDK (we can't change this code) ---
    public record FedExRateRequest(double WeightLbs);
    public class FedExApiException(string message) : Exception(message);

    public class FedExClient
    {
        // Their shape: pounds in, cents out, their own request type and exception.
        public int GetRate(FedExRateRequest request) => (int)(request.WeightLbs * 250);
    }

    // ❌ BEFORE
    // Problem: Checkout talks to the FedEx SDK directly.
    //   - FedEx types and unit conversions (kg→lb, cents→dollars) leak into business code.
    //   - Switching to / adding UPS means rewriting Checkout.
    //   - Can't unit-test Checkout without the real SDK.
    public class CheckoutBefore(FedExClient fedEx)
    {
        public decimal ShippingCost(double weightKg)
        {
            var cents = fedEx.GetRate(new FedExRateRequest(weightKg * 2.20462));
            return cents / 100m;
        }
    }

    // ✅ AFTER

    // OUR interface, in OUR terms (kg in, dollars out). Checkout only knows this.
    public interface IShippingRateProvider
    {
        decimal GetQuote(double weightKg);
    }

    public class ShippingUnavailableException(string message, Exception inner) : Exception(message, inner);

    // The adapter: wraps FedExClient and translates. Only TRANSLATES; no retries, no caching, no rules.
    // How this solves it: all FedEx knowledge (types, units, exceptions) lives in this one class.
    // Adding UPS = a UpsAdapter : IShippingRateProvider. Checkout never changes.
    public class FedExAdapter(FedExClient fedEx) : IShippingRateProvider
    {
        public decimal GetQuote(double weightKg)
        {
            try
            {
                var cents = fedEx.GetRate(new FedExRateRequest(weightKg * 2.20462));   // our units → theirs
                return cents / 100m;                                                    // their units → ours
            }
            catch (FedExApiException ex)
            {
                throw new ShippingUnavailableException("Shipping quote unavailable.", ex); // their error → ours
            }
        }
    }

    // How this solves it: Checkout depends on our interface; it can be tested with a fake provider.
    public class CheckoutAfter(IShippingRateProvider shipping)
    {
        public decimal ShippingCost(double weightKg) => shipping.GetQuote(weightKg);
    }

    // ===== What goes in Program.cs =====
    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<FedExClient>();                            // the third-party SDK
            builder.Services.AddSingleton<IShippingRateProvider, FedExAdapter>();    // swap to UpsAdapter here only
            builder.Services.AddScoped<CheckoutAfter>();

            var app = builder.Build();

            // GET /shipping/quote?weightKg=2
            app.MapGet("/shipping/quote", (double weightKg, CheckoutAfter checkout) =>
                Results.Ok(checkout.ShippingCost(weightKg)));

            app.Run();
        }
    }
}

// Facade (one-liner, for comparison):
//   Adapter = make ONE interface COMPATIBLE (FedEx shape → our shape).
//   Facade  = make MANY classes SIMPLE: e.g. CheckoutFacade.PlaceOrder() calls inventory, payment,
//             shipping, and email in the right order, so the controller makes one call instead of four.
