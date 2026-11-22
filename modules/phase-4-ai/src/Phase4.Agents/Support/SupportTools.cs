using System.ComponentModel;
using Microsoft.Extensions.AI;
using Phase4.Agents.Domain;

namespace Phase4.Agents.Support;

public enum RefundReason { Damaged, NotDelivered, WrongItem }   // an enum: the model can only pick one of these

// The tools the support agent may use. Created PER REQUEST with the user from the JWT, so every tool
// checks ownership with the real user, never with an id the model typed.
// Least privilege: 2 reads + 1 write. No "update order", no "run SQL", no "send email".
public class SupportTools(OrderStore store, string userId, string conversationId, ApprovalGate gate)
{
    public IList<AITool> All =>
    [
        AIFunctionFactory.Create(GetOrder),
        AIFunctionFactory.Create(ListMyOrders),
        AIFunctionFactory.Create(RefundOrder),
    ];

    [Description("Get one of the current customer's orders: item, total, already refunded, status")]
    private async Task<string> GetOrder([Description("The order id, e.g. 123")] string orderId, CancellationToken ct) =>
        await store.GetAsync(orderId, ct) is { } o && o.Owner == userId
            ? $"order {o.Id}: {o.Item}, total {o.Total}, refunded {o.Refunded}, status {o.Status}"
            : "order not found";   // same answer for "doesn't exist" and "not yours": reveals nothing

    [Description("List the current customer's orders")]
    private async Task<string> ListMyOrders(CancellationToken ct) =>
        string.Join("\n", (await store.ListForAsync(userId, ct)).Select(o => $"{o.Id}: {o.Item}, total {o.Total}, status {o.Status}"));

    [Description("Refund part or all of a delivered order. Up to 50 USD in total per order is refunded now; more needs a human's approval.")]
    private async Task<string> RefundOrder(
        [Description("The order id")] string orderId,
        [Description("Amount in USD")] decimal amount,
        RefundReason reason,
        CancellationToken ct)
    {
        if (await store.GetAsync(orderId, ct) is not { } o || o.Owner != userId) return "order not found";
        if (o.Status != "delivered") return $"can't refund: order is {o.Status}, not delivered yet";
        if (amount <= 0 || amount > o.Total - o.Refunded)
            return $"error: amount must be more than 0 and at most {o.Total - o.Refunded}";   // clear, so the model can fix it

        var key = $"{conversationId}:{orderId}:{amount}";   // idempotency key: a repeated call in this chat refunds once

        if (RefundPolicy.Decide(o, amount) == RefundTier.Auto)   // Fix 1: counts what was already refunded on this order
            return await store.RefundAsync(key, orderId, amount, ct);   // tier 1: automatic

        var approvalId = await store.RequestApprovalAsync(key, orderId, amount, reason.ToString(), userId, ct);   // tier 2: a human
        gate.MarkWaiting(approvalId);   // Fix 2: tells the loop to stop taking actions
        return $"waiting for approval {approvalId}: tell the customer a team member will review it within 24 hours";
    }
}
