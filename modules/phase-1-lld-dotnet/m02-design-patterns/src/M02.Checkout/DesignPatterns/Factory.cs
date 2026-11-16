namespace M02.Checkout.DesignPatterns;

// Factory: one place decides WHICH object to create and HOW to build it.
public static class Factory
{
    public interface IPaymentProvider { string Pay(decimal amount); }

    public class StripeProvider(string apiKey) : IPaymentProvider
    {
        public string Pay(decimal amount) => $"Stripe ({apiKey}) paid {amount}";
    }

    public class PayPalProvider(string clientId) : IPaymentProvider
    {
        public string Pay(decimal amount) => $"PayPal ({clientId}) paid {amount}";
    }

    // ❌ Violation: every caller picks and builds the provider itself.
    //    Refunds, Billing, etc. repeat the same if/else and secrets.
    public class CheckoutBefore
    {
        public string Pay(string method, decimal amount)
        {
            IPaymentProvider provider = method == "Card"
                ? new StripeProvider("sk_live_123")
                : new PayPalProvider("pp_client_456");
            return provider.Pay(amount);
        }
    }

    // ✅ Fix: creation logic lives in one factory.
    public interface IPaymentProviderFactory { IPaymentProvider Create(string method); }

    public class PaymentProviderFactory(string stripeKey, string payPalClientId) : IPaymentProviderFactory
    {
        public IPaymentProvider Create(string method) => method switch
        {
            "Card"   => new StripeProvider(stripeKey),
            "PayPal" => new PayPalProvider(payPalClientId),
            _        => throw new NotSupportedException(method)
        };
    }

    public class Checkout(IPaymentProviderFactory factory)
    {
        public string Pay(string method, decimal amount) => factory.Create(method).Pay(amount);
    }

    // How the fix solves it: callers never use `new` on providers. Changing how a provider
    // is built (new secret, HttpClient, retries) is one edit in the factory, not in every caller.
}
