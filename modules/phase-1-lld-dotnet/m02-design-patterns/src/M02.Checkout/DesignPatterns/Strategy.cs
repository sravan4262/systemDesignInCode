namespace M02.Checkout.DesignPatterns;

// Strategy: interchangeable algorithms behind one interface, picked at runtime.
public static class Strategy
{
    public record Cart(decimal Total);

    // ❌ Violation: every new discount = another branch in checkout code.
    public class CheckoutBefore
    {
        public decimal Apply(Cart cart, string code) => code switch
        {
            "PERCENT10" => cart.Total * 0.9m,
            "FLAT20"    => cart.Total - 20,
            _           => cart.Total
        };
    }

    // ✅ Fix: each discount is its own class.
    public interface IDiscount
    {
        string Code { get; }
        decimal Apply(Cart cart);
    }

    public class PercentOff(string code, decimal percent) : IDiscount
    {
        public string Code => code;
        public decimal Apply(Cart cart) => cart.Total * (1 - percent / 100);
    }

    public class FlatOff(string code, decimal amount) : IDiscount
    {
        public string Code => code;
        public decimal Apply(Cart cart) => cart.Total - amount;
    }

    public class Checkout(IEnumerable<IDiscount> discounts)
    {
        public decimal Apply(Cart cart, string code) =>
            discounts.Single(d => d.Code == code).Apply(cart);
    }

    // How the fix solves it: a new discount = a new class + one DI registration.
    // Checkout never changes.
}
