using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Phase4.Agents.Domain;
using Phase4.Agents.Support;

namespace Phase4.Agents.Mcp;

// Step 7: the order tools as an MCP server, at POST /mcp (see Program.cs).
// Any MCP client (VS Code Copilot, Claude Desktop, another team's agent) connects with config only,
// calls tools/list to discover these 3 tools, and tools/call to run them. No integration code per tool.
//
// The tools DELEGATE to SupportTools, so the rules are identical to our own agent's:
// owner check with the caller's identity, refund tiers (> 50 waits for a human), idempotency keys.
// The caller is taken from the request (real app: the OAuth token's user), NEVER from a tool argument,
// so a model can't choose whose orders it touches.
[McpServerToolType]
public class McpOrderTools(OrderStore store, IHttpContextAccessor http)
{
    [McpServerTool(Name = "GetOrder", ReadOnly = true)]
    [Description("Get one of the current customer's orders: item, total, already refunded, status")]
    public Task<string> GetOrder([Description("The order id, e.g. 123")] string orderId, CancellationToken ct) =>
        ForCaller().GetOrder(orderId, ct);

    [McpServerTool(Name = "ListMyOrders", ReadOnly = true)]
    [Description("List the current customer's orders")]
    public Task<string> ListMyOrders(CancellationToken ct) =>
        ForCaller().ListMyOrders(ct);

    // ReadOnly = false + Destructive: clients like VS Code ask the user to confirm before running it.
    // The human-approval tier above 50 stays HERE in the server, whatever the client does.
    [McpServerTool(Name = "RefundOrder", ReadOnly = false, Destructive = true, Idempotent = true)]
    [Description("Refund part or all of a delivered order. Up to 50 USD in total per order is refunded now; more needs a human's approval.")]
    public Task<string> RefundOrder(
        [Description("The order id")] string orderId,
        [Description("Amount in USD")] decimal amount,
        RefundReason reason,
        CancellationToken ct) =>
        ForCaller().RefundOrder(orderId, amount, reason, ct);

    // Real app: MapMcp("/mcp").RequireAuthorization() + OAuth, and the user from the token's claims.
    private SupportTools ForCaller()
    {
        var userId = http.HttpContext?.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(userId)) throw new McpException("Not signed in: send X-User-Id (real app: an OAuth token).");

        // Idempotency scope = this user over MCP. Real app: the client sends an idempotency key per request.
        return new SupportTools(store, userId, $"mcp:{userId}", new ApprovalGate());
    }
}
