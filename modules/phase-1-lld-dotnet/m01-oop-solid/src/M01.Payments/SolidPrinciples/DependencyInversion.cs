namespace M01.Payments.SolidPrinciples;

/// <summary>
/// D — Dependency Inversion: business code and technical details both depend on an abstraction,
/// and the business side owns that abstraction. Injection is a technique; inversion is the principle.
/// </summary>
public static class DependencyInversion
{
    // Low-level detail: a stand-in for the Stripe SDK.
    public class StripeClient
    {
        public string CreateCharge(decimal amount, string currency) => $"ch_{Guid.NewGuid():N}";
    }

    public static class Violation
    {
        // ❌ Business policy creates and depends on a concrete detail: can't swap Stripe, can't test without it.
        public class PaymentService
        {
            private readonly StripeClient _stripe = new();
            public string Pay(decimal amount) => _stripe.CreateCharge(amount, "usd");
        }

        // ❌ Injection WITHOUT inversion: injected, but still depends on the concrete StripeClient.
        public class InjectedPaymentService(StripeClient stripe)
        {
            public string Pay(decimal amount) => stripe.CreateCharge(amount, "usd");
        }
    }

    public static class Fix
    {
        // ✅ Abstraction in BUSINESS terms, owned by the business side (lives in the Application project).
        public interface IPaymentGateway
        {
            string Charge(decimal amount, string currency);
        }

        // ✅ High-level policy depends only on the abstraction (composition: it HAS a gateway).
        public class PaymentService(IPaymentGateway gateway)
        {
            public string Pay(decimal amount) => gateway.Charge(amount, "usd");
        }

        // Details conform to the policy (live in the Infrastructure project, which references Application).
        public class StripePaymentGateway(StripeClient stripe) : IPaymentGateway
        {
            public string Charge(decimal amount, string currency) => stripe.CreateCharge(amount, currency);
        }

        public class AdyenPaymentGateway : IPaymentGateway
        {
            public string Charge(decimal amount, string currency) => $"adyen_{Guid.NewGuid():N}";
        }

        // Test double: no network, runs in milliseconds.
        public class FakePaymentGateway : IPaymentGateway
        {
            public string Charge(decimal amount, string currency) => "ch_fake";
        }

        // Composition root (Program.cs): swap providers by changing one line.
        //   services.AddSingleton<StripeClient>();
        //   services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        //   services.AddScoped<PaymentService>();
    }
}
