namespace M01.Payments.SolidPrinciples;

/// <summary>
/// O — Open/Closed: add new behavior by adding code, not by editing existing, tested code.
/// Also shows Abstraction (the interface) and Polymorphism (the same call, different code at runtime).
/// </summary>
public static class OpenClosed
{
    public static class Violation
    {
        // ❌ Every new payment method means editing this class (and every other switch on "method").
        public class PaymentProcessor
        {
            public string Pay(string method, decimal amount) => method switch
            {
                "Card"   => $"Stripe charged card {amount}",
                "Wallet" => $"PayPal charged wallet {amount}",
                "Bank"   => $"ACH debit started for {amount}",
                _        => throw new NotSupportedException(method)   // "add Apple Pay" = edit here
            };
        }
    }

    public static class Fix
    {
        // Abstraction: WHAT a provider does, not HOW.
        public interface IPaymentProvider
        {
            string Method { get; }          // each provider declares its own key
            string Charge(decimal amount);
        }

        public class CardProvider : IPaymentProvider
        {
            public string Method => "Card";
            public string Charge(decimal amount) => $"Stripe charged card {amount}";
        }

        public class WalletProvider : IPaymentProvider
        {
            public string Method => "Wallet";
            public string Charge(decimal amount) => $"PayPal charged wallet {amount}";
        }

        public class BankTransferProvider : IPaymentProvider
        {
            public string Method => "Bank";
            public string Charge(decimal amount) => $"ACH debit started for {amount}";
        }

        // ✅ Extension: a new method is a new class + one DI registration. PaymentService does not change.
        public class ApplePayProvider : IPaymentProvider
        {
            public string Method => "ApplePay";
            public string Charge(decimal amount) => $"Apple Pay authorized {amount}";
        }

        // ✅ Closed: knows only the interface, never a concrete provider.
        public class PaymentService
        {
            private readonly Dictionary<string, IPaymentProvider> _byMethod;

            // Built once at startup from whatever is registered in DI (throws if two claim the same method).
            public PaymentService(IEnumerable<IPaymentProvider> providers) =>
                _byMethod = providers.ToDictionary(p => p.Method, StringComparer.OrdinalIgnoreCase);

            // Polymorphism: the same call runs different code depending on the object picked at runtime.
            public string Pay(string method, decimal amount) =>
                _byMethod.TryGetValue(method, out var provider)
                    ? provider.Charge(amount)
                    : throw new NotSupportedException($"Unsupported payment method: {method}");
        }

        // Composition root (Program.cs), the one place that knows concrete types:
        //   services.AddSingleton<IPaymentProvider, CardProvider>();
        //   services.AddSingleton<IPaymentProvider, ApplePayProvider>();   // ← the only line added for Apple Pay
        //   services.AddSingleton<PaymentService>();
    }
}
