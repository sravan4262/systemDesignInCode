using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Phase4.Agents.Support;

namespace Phase4.Agents.Controllers;

// Step 7b: an MCP CLIENT in .NET. It knows nothing about orders: no SupportTools, no OrderStore, no tool code.
// All it has is a URL (+ who the user is). It discovers the tools at run time and hands them to the agent.
// Here it happens to point at our own /mcp server; swap the URL and the same code uses any MCP server.
[ApiController]
[Route("mcp-client")]
public class McpClientController(AgentLoop agent, IConfiguration config) : ControllerBase
{
    public record ChatRequest(string Message);

    // GET /mcp-client/tools   header X-User-Id: alice   → what the server says it offers (tools/list)
    [HttpGet("tools")]
    public async Task<IActionResult> Tools([FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct)
    {
        await using var client = await ConnectAsync(userId, ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return Ok(tools.Select(t => new { t.Name, t.Description, schema = t.JsonSchema }));
    }

    // POST /mcp-client/chat   header X-User-Id: alice   body { message }
    // Same AgentLoop as SupportController, but the tools come from the MCP server instead of our own C# classes.
    [HttpPost("chat")]
    public async Task<IActionResult> Chat(ChatRequest req, [FromHeader(Name = "X-User-Id")] string userId, CancellationToken ct)
    {
        await using var client = await ConnectAsync(userId, ct);

        // 1. Discover: each McpClientTool IS an AIFunction, so it plugs straight into the model's tool list.
        //    Calling it = the client sends tools/call to the server; the server runs the real code.
        IList<AITool> tools = [.. await client.ListToolsAsync(cancellationToken: ct)];

        List<ChatMessage> messages = [new(ChatRole.System, SupportController.SystemPrompt), new(ChatRole.User, req.Message)];
        var result = await agent.RunAsync(messages, tools, new AgentBudget(), new ApprovalGate(), ct);

        return Ok(new { answer = result.Answer, stopReason = result.StopReason.ToString(), toolsFromServer = tools.Select(t => t.Name), result.Steps });
    }

    // The whole "integration": a URL + the user's identity. Real app: the user's OAuth token, not a header copy.
    private async Task<McpClient> ConnectAsync(string userId, CancellationToken ct)
    {
        var url = config["Mcp:OrdersUrl"] ?? $"{Request.Scheme}://{Request.Host}/mcp";   // default: our own server
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(url),
            AdditionalHeaders = new Dictionary<string, string> { ["X-User-Id"] = userId },
        });
        return await McpClient.CreateAsync(transport, cancellationToken: ct);   // sends initialize, agrees on capabilities
    }
}
