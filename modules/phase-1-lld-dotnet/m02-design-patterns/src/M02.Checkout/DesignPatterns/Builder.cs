using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace M02.Checkout.DesignPatterns;

// Builder: build a complex object step by step; Build() validates and returns a finished, valid object.
public static class Builder
{
    // ❌ BEFORE
    // Problem: everything goes through one constructor (or public setters).
    //   - Call sites are unclear: new OrderBefore("c1", [], null). What is null? Is [] allowed?
    //   - Nothing stops an invalid order (no items); it fails later, far from the cause.
    //   - Optional data (discount) means passing null everywhere.
    public class OrderBefore(string customerId, List<string> items, string? discountCode)
    {
        public string CustomerId { get; } = customerId;
        public List<string> Items { get; } = items;
        public string? DiscountCode { get; } = discountCode;
    }

    // ✅ AFTER

    // The product: private constructor, read-only, always valid.
    // How this solves it: the ONLY way to get an Order is through the builder's Build().
    public sealed class Order
    {
        public string CustomerId { get; }
        public IReadOnlyList<string> Items { get; }
        public string? DiscountCode { get; }

        private Order(string customerId, List<string> items, string? discountCode)
        {
            CustomerId = customerId;
            Items = [.. items];          // copy, so later builder changes can't touch this order
            DiscountCode = discountCode;
        }

        public static OrderBuilder Create(string customerId) => new(customerId);

        // The builder: collects values step by step with named methods.
        // How this solves it: no mystery nulls, optional steps are just skipped,
        // and Build() checks the rules before an Order can exist.
        public sealed class OrderBuilder(string customerId)
        {
            private readonly List<string> _items = [];
            private string? _discountCode;

            public OrderBuilder AddItem(string sku)       { _items.Add(sku); return this; }
            public OrderBuilder WithDiscount(string code) { _discountCode = code; return this; }

            public Order Build()
            {
                if (_items.Count == 0)
                    throw new InvalidOperationException("An order needs at least one item.");
                return new Order(customerId, _items, _discountCode);
            }
        }
    }

    // ===== What goes in Program.cs =====
    // Nothing to register: a builder is stateful and single-use (one per order).
    // Never make it a DI singleton; two requests would write into the same builder.
    public static class Program
    {
        public record CreateOrderRequest(string CustomerId, List<string> Items, string? DiscountCode);

        public static void Run(string[] args)
        {
            var app = WebApplication.CreateBuilder(args).Build();   // .NET's own Builder pattern

            // POST /orders  { "customerId": "c1", "items": ["SKU1"], "discountCode": "FLAT20" }
            app.MapPost("/orders", (CreateOrderRequest req) =>
            {
                var builder = Order.Create(req.CustomerId);
                foreach (var sku in req.Items) builder.AddItem(sku);
                if (req.DiscountCode is not null) builder.WithDiscount(req.DiscountCode);   // optional step

                try   { return Results.Ok(builder.Build()); }
                catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
            });

            app.Run();
        }
    }
}
