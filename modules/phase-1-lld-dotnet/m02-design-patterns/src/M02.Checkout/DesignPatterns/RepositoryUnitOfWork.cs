using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace M02.Checkout.DesignPatterns;

// Repository: a collection-like class that returns domain objects and hides HOW they're stored/queried.
// Unit of Work: tracks changes during one operation and saves them together in ONE transaction, or none.
// The critique: EF Core already IS both. DbSet<T> = repository, DbContext (change tracker + SaveChanges) = UoW.
public static class RepositoryUnitOfWork
{
    // Shared by Before and After: entities + the EF DbContext.
    public class Order
    {
        public int Id { get; set; }
        public required string CustomerId { get; set; }
        public string Status { get; set; } = "Pending";
    }

    public class Stock
    {
        public required string Sku { get; set; }   // key
        public int Quantity { get; set; }
    }

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();   // already a repository
        public DbSet<Stock> Stock => Set<Stock>();

        protected override void OnModelCreating(ModelBuilder b) => b.Entity<Stock>().HasKey(s => s.Sku);
    }

    // ❌ BEFORE: generic repository + unit of work wrapped around EF Core
    // Problem: two layers that copy what EF already does.
    //   - IRepository<T> is DbSet<T> with fewer features (no Include, AsNoTracking, projections).
    //   - Returning IQueryable leaks EF anyway: callers still write EF queries, just through a wrapper.
    //   - IUnitOfWork.Commit() is literally SaveChanges(). Ceremony, no new guarantee.
    //   - Every new entity = another property on the UoW.
    public interface IRepository<T> where T : class
    {
        IQueryable<T> Query();
        void Add(T entity);
    }

    public class GenericRepository<T>(AppDbContext db) : IRepository<T> where T : class
    {
        public IQueryable<T> Query() => db.Set<T>();   // just forwards to DbSet
        public void Add(T entity) => db.Set<T>().Add(entity);
    }

    public class EfUnitOfWork(AppDbContext db) : IDisposable
    {
        public IRepository<Order> Orders { get; } = new GenericRepository<Order>(db);
        public IRepository<Stock> Stock { get; } = new GenericRepository<Stock>(db);

        public Task<int> CommitAsync() => db.SaveChangesAsync();   // just forwards to SaveChanges
        public void Dispose() => db.Dispose();
    }

    // ✅ AFTER (option A, the default): use DbContext directly.
    // How this solves it: no wrapper layers. DbSet is the repository, SaveChanges is the unit of work:
    // the new order and the stock decrement are saved together in one transaction, or neither is.
    public class PlaceOrderService(AppDbContext db)
    {
        public async Task<int> PlaceAsync(string customerId, string sku)
        {
            var stock = await db.Stock.SingleAsync(s => s.Sku == sku);
            if (stock.Quantity == 0) throw new InvalidOperationException("Out of stock");

            stock.Quantity--;                                   // change #1 (tracked)
            var order = new Order { CustomerId = customerId };
            db.Orders.Add(order);                               // change #2 (tracked)

            await db.SaveChangesAsync();                        // ONE commit for both (the unit of work)
            return order.Id;
        }
    }

    // ✅ AFTER (option B, when it earns its keep): a SPECIFIC repository with named queries.
    // How this solves it: the query logic lives in one named, reusable place instead of being copied
    // across services. It returns domain objects, not IQueryable. Saving still goes through DbContext.
    public interface IOrderRepository
    {
        Task<List<Order>> GetPendingForCustomerAsync(string customerId);
    }

    public class EfOrderRepository(AppDbContext db) : IOrderRepository
    {
        public Task<List<Order>> GetPendingForCustomerAsync(string customerId) =>
            db.Orders.AsNoTracking()                            // EF feature a generic repo would hide
                     .Where(o => o.CustomerId == customerId && o.Status == "Pending")
                     .ToListAsync();
    }

    // ===== What goes in Program.cs =====
    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Scoped by default: one DbContext (= one unit of work) per request. Never a singleton.
            // Demo uses the in-memory provider; real app: UseSqlServer(...). (In-memory has no real
            // transactions; on SQL Server, SaveChanges wraps all tracked changes in one transaction.)
            builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("m02"));
            builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
            builder.Services.AddScoped<PlaceOrderService>();

            var app = builder.Build();

            // POST /orders?customerId=c1&sku=SKU1
            app.MapPost("/orders", async (string customerId, string sku, PlaceOrderService orders) =>
                Results.Ok(await orders.PlaceAsync(customerId, sku)));

            // GET /customers/c1/orders/pending
            app.MapGet("/customers/{customerId}/orders/pending", async (string customerId, IOrderRepository repo) =>
                Results.Ok(await repo.GetPendingForCustomerAsync(customerId)));

            app.Run();
        }
    }
}

// When a repository earns its keep:
//   - Named, reusable queries (GetPendingForCustomer) instead of the same LINQ copied across services.
//   - DDD: one repository per aggregate, domain layer free of EF.
//   - More than one data source behind one interface (SQL + external API).
// When it doesn't: a generic IRepository<T> / IUnitOfWork that only forwards to DbSet / SaveChanges.
// Testing argument ("we need it to mock the DB")? Prefer testing against a real DB (Testcontainers / SQLite).
