namespace M01.Payments.SolidPrinciples;

/// <summary>
/// I — Interface Segregation: don't force a class to depend on methods it doesn't use.
/// Split interfaces by what each CLIENT needs.
/// </summary>
public static class InterfaceSegregation
{
    public sealed record Plan(string Name, decimal MonthlyPrice);

    public static class Violation
    {
        // ❌ Fat interface: every provider must implement all of it; every client depends on all of it.
        public interface IPaymentProvider
        {
            string Charge(decimal amount);
            void Refund(decimal amount);
            string Authorize(decimal amount);
            void Capture(string authorizationId);
            string CreateSubscription(Plan plan);
        }

        // ❌ Forced to "implement" methods it can't honor → throws → LSP violation waiting to happen.
        public class ApplePayProvider : IPaymentProvider
        {
            public string Charge(decimal amount) => $"Apple Pay charged {amount}";
            public void Refund(decimal amount) => Console.WriteLine($"Apple Pay refunded {amount}");
            public string Authorize(decimal amount) => throw new NotImplementedException();
            public void Capture(string authorizationId) => throw new NotImplementedException();
            public string CreateSubscription(Plan plan) => throw new NotImplementedException();
        }

        // ❌ Needs only Refund, but depends on (and must fake in tests) all five methods.
        //    A change to CreateSubscription's signature forces this class to recompile.
        public class RefundService(IPaymentProvider provider)
        {
            public void RefundOrder(decimal amount) => provider.Refund(amount);
        }
    }

    public static class Fix
    {
        // ✅ Small role interfaces, shaped by what clients need.
        public interface IChargeable { string Charge(decimal amount); }
        public interface IRefundable { void Refund(decimal amount); }
        public interface IAuthorizeCapture
        {
            string Authorize(decimal amount);
            void Capture(string authorizationId);
        }
        public interface ISubscriptionBilling { string CreateSubscription(Plan plan); }

        // ✅ Each class implements only the roles it can fully honor.
        public class CardProvider : IChargeable, IRefundable, IAuthorizeCapture, ISubscriptionBilling
        {
            public string Charge(decimal amount) => $"Card charged {amount}";
            public void Refund(decimal amount) => Console.WriteLine($"Card refunded {amount}");
            public string Authorize(decimal amount) => $"auth_{amount}";
            public void Capture(string authorizationId) => Console.WriteLine($"Captured {authorizationId}");
            public string CreateSubscription(Plan plan) => $"sub_{plan.Name}";
        }

        public class ApplePayProvider : IChargeable, IRefundable
        {
            public string Charge(decimal amount) => $"Apple Pay charged {amount}";
            public void Refund(decimal amount) => Console.WriteLine($"Apple Pay refunded {amount}");
        }

        // ✅ Each client depends only on the role it uses.
        public class RefundService(IRefundable refunds)
        {
            public void RefundOrder(decimal amount) => refunds.Refund(amount);
        }

        public class BillingService(ISubscriptionBilling billing)
        {
            public string Subscribe(Plan plan) => billing.CreateSubscription(plan);
        }
    }
}
