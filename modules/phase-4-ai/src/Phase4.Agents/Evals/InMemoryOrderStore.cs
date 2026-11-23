using System.Security.Cryptography;
using System.Text;
using Phase4.Agents.Domain;

namespace Phase4.Agents.Evals;

// The same 3 orders as schema.sql, in memory. A NEW one per trial, so trial 2 never sees trial 1's refund.
// Same return texts as OrderStore, so the model sees exactly what it would see in production.
// It also records what happened (Refunds, Pending), so cases can check the RESULT, not only the words.
// Not thread-safe: one trial = one agent = calls one at a time.
public class InMemoryOrderStore : IOrderStore
{
    private readonly Dictionary<string, Order> _orders = new()
    {
        ["123"] = new("123", "alice", "running shoes", 120, 0, "delivered"),
        ["124"] = new("124", "alice", "headphones", 40, 0, "delivered"),
        ["456"] = new("456", "bob", "laptop", 1500, 0, "shipped"),
    };
    private readonly Dictionary<string, string> _doneByKey = [];

    public List<(string OrderId, decimal Amount)> Refunds { get; } = [];
    public List<(string ApprovalId, string OrderId, decimal Amount)> Pending { get; } = [];

    public Task<Order?> GetAsync(string orderId, CancellationToken ct) => Task.FromResult(_orders.GetValueOrDefault(orderId));

    public Task<List<Order>> ListForAsync(string owner, CancellationToken ct) =>
        Task.FromResult(_orders.Values.Where(o => o.Owner == owner).ToList());

    public Task<string> RefundAsync(string idempotencyKey, string orderId, decimal amount, CancellationToken ct)
    {
        if (_doneByKey.TryGetValue(idempotencyKey, out var earlier)) return Task.FromResult(earlier);   // same key → same result, once
        var o = _orders[orderId];
        if (o.Total - o.Refunded < amount) return Task.FromResult($"can't refund {amount}: not that much left on order {orderId}");

        _orders[orderId] = o with { Refunded = o.Refunded + amount };
        Refunds.Add((orderId, amount));
        return Task.FromResult(_doneByKey[idempotencyKey] = $"refunded {amount} on order {orderId}");
    }

    public Task<string> RequestApprovalAsync(string idempotencyKey, string orderId, decimal amount, string reason, string userId, CancellationToken ct)
    {
        var id = "apr-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)))[..12].ToLowerInvariant();
        if (!Pending.Any(p => p.ApprovalId == id)) Pending.Add((id, orderId, amount));
        return Task.FromResult(id);
    }
}
