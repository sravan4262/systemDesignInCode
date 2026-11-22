using Microsoft.Extensions.AI;
using Phase4.Agents.Domain;
using Phase4.Agents.Support;

namespace Phase4.Agents.MultiAgent;

public enum WorkerKind { Orders, Refunds, Policy }

// A worker = the same AgentLoop, with its own short instructions and ONLY the tools for its job.
// Tools are built per worker with its own ApprovalGate (workers run in parallel, so they can't share one).
public record Worker(WorkerKind Kind, string Instructions, Func<ApprovalGate, IList<AITool>> Tools);

public static class Workers
{
    // Built per request for the user from the JWT: every worker's tools check ownership with that user.
    public static Dictionary<WorkerKind, Worker> For(OrderStore store, string userId, string conversationId)
    {
        IList<AITool> Pick(ApprovalGate gate, params string[] names) =>
            new SupportTools(store, userId, conversationId, gate).All.Where(t => names.Contains(t.Name)).ToList();

        return new()
        {
            [WorkerKind.Orders] = new(WorkerKind.Orders,
                "You answer questions about the customer's orders: status, items, totals. Use the tools. Reply in 1–2 sentences.",
                gate => Pick(gate, "GetOrder", "ListMyOrders")),                 // read-only

            [WorkerKind.Refunds] = new(WorkerKind.Refunds,
                "You handle refund requests for delivered orders. Look up the order, then refund. " +
                "If a refund waits for approval, say so; never say it was refunded. Reply in 1–2 sentences.",
                gate => Pick(gate, "GetOrder", "RefundOrder")),                 // the only worker that can write

            [WorkerKind.Policy] = new(WorkerKind.Policy,
                "You answer questions about return policies. Use the tool. Reply in 1 sentence.",
                _ => new PolicyTools().All),                                    // no customer data at all
        };
    }
}
