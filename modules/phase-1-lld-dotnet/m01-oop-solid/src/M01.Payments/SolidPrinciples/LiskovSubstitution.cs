namespace M01.Payments.SolidPrinciples;

/// <summary>
/// L — Liskov Substitution: any subtype must work wherever its base type is expected.
/// Changing HOW is polymorphism; breaking WHAT was promised is a violation.
/// </summary>
public static class LiskovSubstitution
{
    public static class Violation
    {
        // Contract: Charge accepts any amount > 0; Refund refunds.
        public interface IPaymentProvider
        {
            string Charge(decimal amount);
            void Refund(decimal amount);
        }

        public class CardProvider : IPaymentProvider
        {
            public string Charge(decimal amount) => $"Card charged {amount}";
            public void Refund(decimal amount) => Console.WriteLine($"Card refunded {amount}");
        }

        // ❌ Compiles, but breaks the contract: Refund was promised, it throws instead.
        public class BankTransferProvider : IPaymentProvider
        {
            public string Charge(decimal amount) => $"ACH debit started for {amount}";
            public void Refund(decimal amount) =>
                throw new NotSupportedException("Bank transfers can't be refunded instantly.");
        }

        // ❌ Strengthened precondition: demands amount >= 1 when the contract allows any amount > 0.
        public class WalletProvider : IPaymentProvider
        {
            public string Charge(decimal amount) =>
                amount < 1 ? throw new ArgumentOutOfRangeException(nameof(amount)) : $"Wallet charged {amount}";
            public void Refund(decimal amount) => Console.WriteLine($"Wallet refunded {amount}");
        }

        // Works for Card, crashes at runtime for BankTransfer. The caller did nothing wrong.
        public class RefundService(IPaymentProvider provider)
        {
            public void RefundOrder(decimal amount) => provider.Refund(amount);
        }
    }

    public static class Fix
    {
        // ✅ Contracts that every implementation can honor. The minimum is now part of the contract.
        public interface IPaymentProvider
        {
            decimal MinimumAmount { get; }   // callers can check it; no hidden precondition
            string Charge(decimal amount);
        }

        public interface IRefundableProvider : IPaymentProvider
        {
            void Refund(decimal amount);
        }

        public class CardProvider : IRefundableProvider
        {
            public decimal MinimumAmount => 0.01m;
            public string Charge(decimal amount) => $"Card charged {amount}";
            public void Refund(decimal amount) => Console.WriteLine($"Card refunded {amount}");
        }

        public class WalletProvider : IRefundableProvider
        {
            public decimal MinimumAmount => 1m;   // declared openly instead of a surprise exception
            public string Charge(decimal amount) => $"Wallet charged {amount}";
            public void Refund(decimal amount) => Console.WriteLine($"Wallet refunded {amount}");
        }

        // ✅ Promises only what it can do: no Refund method to break.
        public class BankTransferProvider : IPaymentProvider
        {
            public decimal MinimumAmount => 0.01m;
            public string Charge(decimal amount) => $"ACH debit started for {amount}";
        }

        // ✅ The compiler now rejects passing a BankTransferProvider here. Runtime crash → compile error.
        public class RefundService(IRefundableProvider provider)
        {
            public void RefundOrder(decimal amount) => provider.Refund(amount);
        }
    }
}
