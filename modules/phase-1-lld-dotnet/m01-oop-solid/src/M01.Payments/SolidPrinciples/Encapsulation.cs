namespace M01.Payments.SolidPrinciples;

/// <summary>
/// Encapsulation: keep state private and change it only through methods that enforce the rules.
/// </summary>
public static class Encapsulation
{
    public static class Violation
    {
        // ❌ Public setters and a mutable list: any caller can put a payment into an invalid state.
        public class Payment
        {
            public decimal Amount { get; set; }
            public string Status { get; set; } = "Pending";
            public List<decimal> Refunds { get; } = new();   // no setter, but the list itself is still mutable
        }

        // All of these compile:
        //   payment.Amount = -50m;
        //   payment.Status = "Refunded";        // never captured
        //   payment.Refunds.Add(9_999m);        // bypasses every refund rule
    }

    public static class Fix
    {
        public enum PaymentStatus { Pending, Captured, Refunded }

        // Immutable item: callers can't change a refund after it is recorded.
        public sealed record RefundEntry(decimal Amount, DateTimeOffset At);

        // ✅ State is private; the only way to change it is through methods that check the rules.
        public sealed class Payment
        {
            private readonly List<RefundEntry> _refunds = new();

            public Payment(decimal amount)
            {
                if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
                Amount = amount;
            }

            public decimal Amount { get; }
            public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
            public decimal RefundedAmount => _refunds.Sum(r => r.Amount);

            // Read-only view: callers can look but not Add/Clear.
            public IReadOnlyList<RefundEntry> Refunds => _refunds.AsReadOnly();

            public void Capture()
            {
                if (Status != PaymentStatus.Pending)
                    throw new InvalidOperationException($"Cannot capture a {Status} payment.");
                Status = PaymentStatus.Captured;
            }

            public void Refund(decimal amount)
            {
                if (Status != PaymentStatus.Captured)
                    throw new InvalidOperationException("Only captured payments can be refunded.");
                if (amount <= 0 || RefundedAmount + amount > Amount)
                    throw new ArgumentOutOfRangeException(nameof(amount), "Refund exceeds the captured amount.");

                _refunds.Add(new RefundEntry(amount, DateTimeOffset.UtcNow));
                if (RefundedAmount == Amount) Status = PaymentStatus.Refunded;
            }
        }
    }
}
