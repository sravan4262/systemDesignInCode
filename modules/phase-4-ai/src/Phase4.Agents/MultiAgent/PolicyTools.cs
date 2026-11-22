using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Phase4.Agents.MultiAgent;

// The policy worker's only tool. Read-only, no customer data at all.
// (Fixed data here; a real app would call the RAG service from build 1.)
public class PolicyTools
{
    private static readonly Dictionary<string, int> ReturnDays = new() { ["shoes"] = 30, ["electronics"] = 15, ["clothing"] = 30 };

    public IList<AITool> All => [AIFunctionFactory.Create(GetReturnPolicy)];

    [Description("Get the return window in days for a product category")]
    private string GetReturnPolicy([Description("shoes, electronics or clothing")] string category) =>
        ReturnDays.TryGetValue(category.ToLowerInvariant(), out var days)
            ? $"{category}: returns accepted within {days} days of delivery"
            : $"no policy found for '{category}'; known categories: {string.Join(", ", ReturnDays.Keys)}";
}
