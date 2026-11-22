using Phase4.Agents.Domain;

namespace Phase4.Agents.Support;

public enum RefundTier { Auto, NeedsApproval }

// Fix 1 (test case #1): the auto limit is per ORDER, not per call.
// Before: each call was checked alone, so RefundOrder(123, 40) three times = 120 refunded with no human.
// After:  already refunded + this amount must stay ≤ 50, so the 2nd call of 40 (40 + 40 = 80) needs a human.
public static class RefundPolicy
{
    public const decimal AutoRefundLimit = 50m;

    public static RefundTier Decide(Order order, decimal amount) =>
        order.Refunded + amount <= AutoRefundLimit ? RefundTier.Auto : RefundTier.NeedsApproval;
}
