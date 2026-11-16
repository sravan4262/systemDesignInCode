using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace M02.Checkout.DesignPatterns;

// Factory: one place decides WHICH object to create and HOW to build it.
public static class Factory
{
    // Shared by Before and After: the products the factory creates.
    // Note: building them needs secrets. That "how to build it" knowledge is what we want in ONE place.
    public interface IPaymentProvider
    {
        string Pay(decimal amount);
        string Refund(decimal amount);
    }

    public class StripeProvider(string apiKey) : IPaymentProvider
    {
        public string Pay(decimal amount)    => $"Stripe ({apiKey[..4]}…) paid {amount}";
        public string Refund(decimal amount) => $"Stripe ({apiKey[..4]}…) refunded {amount}";
    }

    public class PayPalProvider(string clientId, string clientSecret) : IPaymentProvider
    {
        public string Pay(decimal amount)    => $"PayPal ({clientId}, secret {clientSecret.Length} chars) paid {amount}";
        public string Refund(decimal amount) => $"PayPal ({clientId}, secret {clientSecret.Length} chars) refunded {amount}";
    }

    // ❌ BEFORE
    // Problem: every caller picks AND builds the provider itself.
    //   - The same if/else is copied into Checkout and Refunds (and Billing, Subscriptions, ...).
    //   - Secrets are hardcoded in every caller.
    //   - Stripe's constructor changes (needs HttpClient, retry policy) → edit every caller.
    //   - Adding Apple Pay → find every copy of the if/else; miss one = production bug.
    public class CheckoutBefore
    {
        public string Pay(string method, decimal amount)
        {
            IPaymentProvider provider = method == "Card"
                ? new StripeProvider("sk_live_123")
                : new PayPalProvider("pp_client_456", "pp_secret_789");
            return provider.Pay(amount);
        }
    }

    public class RefundsBefore
    {
        public string Refund(string method, decimal amount)
        {
            // Same selection + construction, copied. Same secrets again. Add Apple Pay in Checkout
            // but forget here → refunds for Apple Pay silently go to PayPal.
            IPaymentProvider provider = method == "Card"
                ? new StripeProvider("sk_live_123")
                : new PayPalProvider("pp_client_456", "pp_secret_789");
            return provider.Refund(amount);
        }
    }

    // ✅ AFTER

    // The factory interface: callers ask "give me a provider for this method".
    // How this solves it: callers depend on this, never on `new StripeProvider(...)`.
    public interface IPaymentProviderFactory
    {
        IPaymentProvider Create(string method);
    }

    // The factory: the ONLY class that knows which provider matches which method
    // and how to build each one (secrets, constructor args).
    // How this solves it: new secret / new constructor arg / Apple Pay = one edit here.
    // (The switch is fine while it's small and in one place. If it grows, swap it for a
    //  Dictionary<string, Func<IPaymentProvider>> — callers don't notice, they only see the interface.)
    public class PaymentProviderFactory(PaymentSettings settings) : IPaymentProviderFactory
    {
        public IPaymentProvider Create(string method) => method switch
        {
            "Card"   => new StripeProvider(settings.StripeApiKey),
            "PayPal" => new PayPalProvider(settings.PayPalClientId, settings.PayPalClientSecret),
            _        => throw new NotSupportedException($"Payment method '{method}' is not supported.")
            // Fail loudly on unknown input; never return null.
        };
    }

    // Secrets come from configuration (user-secrets / env vars), not from code.
    public record PaymentSettings(string StripeApiKey, string PayPalClientId, string PayPalClientSecret);

    // Callers: no `new`, no if/else, no secrets. They just ask the factory.
    // How this solves it: Checkout and Refunds can't drift apart; there's nothing to copy.
    public class CheckoutAfter(IPaymentProviderFactory factory)
    {
        public string Pay(string method, decimal amount) => factory.Create(method).Pay(amount);
    }

    public class RefundsAfter(IPaymentProviderFactory factory)
    {
        public string Refund(string method, decimal amount) => factory.Create(method).Refund(amount);
    }

    // ===== What goes in Program.cs (the composition root) =====
    // Reads the secrets once and registers the factory. It does NOT create providers;
    // that's the factory's job, per request, based on the method the user picked.
    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Settings from config (appsettings / user-secrets / env vars). Fail fast at startup if missing.
            var config = builder.Configuration;
            var settings = new PaymentSettings(
                config["Payments:StripeApiKey"]       ?? throw new InvalidOperationException("Missing Payments:StripeApiKey"),
                config["Payments:PayPalClientId"]     ?? throw new InvalidOperationException("Missing Payments:PayPalClientId"),
                config["Payments:PayPalClientSecret"] ?? throw new InvalidOperationException("Missing Payments:PayPalClientSecret"));
            builder.Services.AddSingleton(settings);

            // 2. The factory. Singleton: it holds only settings and creates a fresh provider per Create().
            builder.Services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

            // 3. The callers. Both get the SAME factory injected.
            builder.Services.AddSingleton<CheckoutAfter>();
            builder.Services.AddSingleton<RefundsAfter>();

            var app = builder.Build();

            // 4. Endpoints. Which provider to use is only known NOW, from the request,
            //    which is why we inject a factory instead of a single IPaymentProvider.
            //    POST /checkout/pay?method=Card&amount=100
            app.MapPost("/checkout/pay", (string method, decimal amount, CheckoutAfter checkout) =>
                Results.Ok(checkout.Pay(method, amount)));

            //    POST /refunds?method=PayPal&amount=40
            app.MapPost("/refunds", (string method, decimal amount, RefundsAfter refunds) =>
                Results.Ok(refunds.Refund(method, amount)));

            app.Run();
        }
    }
}
