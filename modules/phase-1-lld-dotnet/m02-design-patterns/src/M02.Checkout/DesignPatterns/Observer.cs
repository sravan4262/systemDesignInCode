using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M02.Checkout.DesignPatterns;

// Observer: when something happens, notify a list of subscribers without knowing who they are.
public static class Observer
{
    public interface IEmailSender     { void Send(string to, string message); }
    public interface IInventory       { void Reserve(string orderId); }

    // ❌ BEFORE
    // Problem: OrderService calls every side effect directly.
    //   - It depends on email, inventory, ... and grows with each new reaction.
    //   - Adding loyalty points = editing OrderService again (breaks Open/Closed).
    //   - The core action (place the order) is tangled with things that merely REACT to it.
    public class OrderServiceBefore(IEmailSender email, IInventory inventory)
    {
        public string PlaceOrder(string customerEmail)
        {
            var orderId = Guid.NewGuid().ToString();   // pretend: save to DB
            email.Send(customerEmail, $"Order {orderId} confirmed");
            inventory.Reserve(orderId);
            // loyalty.Award(...)  ← next month, another edit here
            return orderId;
        }
    }

    // ✅ AFTER

    // The event: a plain record describing WHAT happened (past tense).
    public record OrderPlaced(string OrderId, string CustomerEmail);

    // The observer contract: "I want to know when an order is placed."
    public interface IOrderPlacedHandler
    {
        void Handle(OrderPlaced e);
    }

    // Observer #1. How this solves it: each reaction is its own class; OrderService doesn't know it exists.
    public class SendConfirmationEmail(IEmailSender email) : IOrderPlacedHandler
    {
        public void Handle(OrderPlaced e) => email.Send(e.CustomerEmail, $"Order {e.OrderId} confirmed");
    }

    // Observer #2. Adding LoyaltyHandler later = a new class + one registration. OrderService untouched.
    public class ReserveInventory(IInventory inventory) : IOrderPlacedHandler
    {
        public void Handle(OrderPlaced e) => inventory.Reserve(e.OrderId);
    }

    // The subject (publisher): does its core job, then notifies whoever is subscribed.
    // How this solves it: depends only on IOrderPlacedHandler, never on email/inventory/loyalty.
    public class OrderServiceAfter(IEnumerable<IOrderPlacedHandler> handlers, ILogger<OrderServiceAfter> logger)
    {
        public string PlaceOrder(string customerEmail)
        {
            var orderId = Guid.NewGuid().ToString();   // pretend: save to DB
            var e = new OrderPlaced(orderId, customerEmail);

            foreach (var handler in handlers)
            {
                // Decision: one failing observer must NOT fail the order or stop the others.
                try { handler.Handle(e); }
                catch (Exception ex) { logger.LogError(ex, "{Handler} failed for {OrderId}", handler.GetType().Name, orderId); }
            }
            // Guarantee check: if the process crashes right after the save, NO handler runs.
            // In-process observers are best-effort. Need "at least once"? → Outbox + broker (M11).
            return orderId;
        }
    }

    // ===== What goes in Program.cs =====
    public class FakeEmailSender : IEmailSender { public void Send(string to, string message) { } }
    public class FakeInventory   : IInventory   { public void Reserve(string orderId) { } }

    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<IEmailSender, FakeEmailSender>();
            builder.Services.AddSingleton<IInventory, FakeInventory>();

            // Subscribe = register. Unsubscribe = delete the line. OrderService never changes.
            builder.Services.AddScoped<IOrderPlacedHandler, SendConfirmationEmail>();
            builder.Services.AddScoped<IOrderPlacedHandler, ReserveInventory>();

            builder.Services.AddScoped<OrderServiceAfter>();

            var app = builder.Build();

            // POST /orders?customerEmail=a@b.com
            app.MapPost("/orders", (string customerEmail, OrderServiceAfter orders) =>
                Results.Ok(orders.PlaceOrder(customerEmail)));

            app.Run();
        }
    }
}

// C# built-in Observer (for comparison): the `event` keyword.
//   public event Action<OrderPlaced>? OrderPlacedEvent;   // subject
//   service.OrderPlacedEvent += e => ...;                  // subscribe
//   OrderPlacedEvent?.Invoke(e);                           // notify
// Pitfall: a long-lived subject holds references to subscribers → memory leak unless they `-=`.
