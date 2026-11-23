using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Phase4.ProductionAi.Security;

// Every tool call is recorded so the response shows what the model actually DID.
public record ToolCallLog(string Tool, string Arguments, string Result);

// ❌ UNSAFE tools: the model chooses WHO (customerId) and WHERE (to). An injected instruction can use both.
public class UnsafeTools(List<ToolCallLog> log)
{
    public IList<AITool> All => [AIFunctionFactory.Create(GetCustomerOrders), AIFunctionFactory.Create(SendEmail)];

    [Description("Get the orders of any customer")]
    public string GetCustomerOrders(string customerId)
    {
        var result = string.Join("; ", SecurityData.Orders.Where(o => o.CustomerId == customerId).Select(o => $"{o.Item} {o.Total} ({o.Email})"));
        log.Add(new(nameof(GetCustomerOrders), customerId, result == "" ? "none" : result));
        return result;
    }

    [Description("Send an email")]
    public string SendEmail(string to, string body)
    {
        log.Add(new(nameof(SendEmail), $"to={to}", $"SENT to {to}: {body}"));
        return $"sent to {to}";
    }
}

// ✅ SAFE tools: identity and destination come from the Caller (the JWT), NOT from arguments.
//   - no "any customer" parameter: the tool can only ever read the caller's own orders (action-time authZ);
//   - no "to" parameter: email can only go to the caller's own address (the exfiltration channel is gone).
// A fooled model can still CALL them; it just can't point them at someone else's data or an outside address.
public class SafeTools(Caller caller, List<ToolCallLog> log)
{
    public IList<AITool> All => [AIFunctionFactory.Create(GetMyOrders), AIFunctionFactory.Create(EmailMeASummary)];

    [Description("Get the current customer's own orders")]
    public string GetMyOrders()
    {
        var result = string.Join("; ", SecurityData.Orders
            .Where(o => o.CustomerId == caller.UserId && o.TenantId == caller.TenantId)
            .Select(o => $"{o.Item} {o.Total}"));
        log.Add(new(nameof(GetMyOrders), $"(caller={caller.UserId})", result == "" ? "none" : result));
        return result;
    }

    [Description("Email a short summary to the current customer's own address")]
    public string EmailMeASummary(string body)
    {
        log.Add(new(nameof(EmailMeASummary), $"to={caller.Email} (fixed)", $"SENT to {caller.Email}"));
        return $"sent to {caller.Email}";
    }
}
