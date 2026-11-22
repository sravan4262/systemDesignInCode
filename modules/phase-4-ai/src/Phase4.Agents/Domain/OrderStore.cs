using System.Security.Cryptography;
using System.Text;

namespace Phase4.Agents.Domain;

public record Order(string Id, string Owner, string Item, decimal Total, decimal Refunded, string Status);
public record PendingRefund(string Id, string OrderId, decimal Amount, string Reason, string RequestedFor);

// In-memory orders + refunds (a real app: the Orders service / DB). The tools call this; the model never does.
public class OrderStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Order> _orders = new()
    {
        ["123"] = new("123", "alice", "running shoes", 120m, 0m, "delivered"),
        ["124"] = new("124", "alice", "headphones",     40m, 0m, "delivered"),
        ["456"] = new("456", "bob",   "laptop",       1500m, 0m, "shipped"),
    };
    private readonly Dictionary<string, string> _doneRefunds = [];       // idempotency key → result
    private readonly Dictionary<string, PendingRefund> _pending = [];    // approval id → refund waiting for a human

    public Order? Get(string orderId) { lock (_lock) return _orders.GetValueOrDefault(orderId); }

    public List<Order> ListFor(string owner) { lock (_lock) return _orders.Values.Where(o => o.Owner == owner).ToList(); }

    // Idempotent: the same key twice (the model retried, or the HTTP call timed out and was resent) refunds ONCE.
    public string Refund(string idempotencyKey, string orderId, decimal amount)
    {
        lock (_lock)
        {
            if (_doneRefunds.TryGetValue(idempotencyKey, out var earlier)) return earlier;
            var o = _orders[orderId];
            if (amount > o.Total - o.Refunded) return $"can't refund {amount}: only {o.Total - o.Refunded} left";
            _orders[orderId] = o with { Refunded = o.Refunded + amount };
            return _doneRefunds[idempotencyKey] = $"refunded {amount} on order {orderId}";
        }
    }

    public string RequestApproval(string idempotencyKey, string orderId, decimal amount, string reason, string userId)
    {
        lock (_lock)
        {
            // same request twice → same approval id (a stable hash; string.GetHashCode changes every process start)
            var id = "apr-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)))[..12].ToLowerInvariant();
            _pending.TryAdd(id, new(id, orderId, amount, reason, userId));
            return id;
        }
    }

    // A human (support role) approves → the refund runs, with the approval id as its idempotency key.
    public string? Approve(string approvalId)
    {
        PendingRefund? p;
        lock (_lock) if (!_pending.Remove(approvalId, out p)) return null;
        return Refund(approvalId, p.OrderId, p.Amount);
    }
}
