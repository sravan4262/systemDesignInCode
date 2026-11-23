using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Phase4.Agents.Domain;
using Phase4.Agents.Evals;
using Phase4.Agents.Mcp;
using Phase4.Agents.Memory;
using Phase4.Agents.MultiAgent;
using Phase4.Agents.Support;
using Phase4.Agents.Traces;
using Phase4.Agents.Workflow;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Key in user-secrets: dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
var apiKey = builder.Configuration["OpenAI:ApiKey"] ?? "missing-key";

// A PLAIN chat client: no UseFunctionInvocation(). AgentLoop runs the tools itself, so it can enforce
// max steps, budgets, repeat detection and approval tiers.
builder.Services.AddChatClient(new ChatClient(builder.Configuration["OpenAI:Model"] ?? "gpt-4.1-mini", apiKey).AsIChatClient());

builder.Services.AddSingleton<OrderStore>();
builder.Services.AddSingleton<AgentLoop>();
builder.Services.AddSingleton<TicketWorkflow>();
builder.Services.AddSingleton<Orchestrator>();
builder.Services.AddSingleton<MemoryStore>();
builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddSingleton<ContextBuilder>();
builder.Services.AddSingleton<CaseStore>();
builder.Services.AddSingleton<AgentTraceStore>();
builder.Services.AddSingleton<EvalRunner>();

// Step 7: the same order tools as an MCP server (Streamable HTTP at /mcp). Stateless: every request stands
// alone (no server-side session), so any instance behind a load balancer can serve it.
builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<McpOrderTools>();

var app = builder.Build();
app.MapControllers();
app.MapMcp("/mcp");   // real app: .RequireAuthorization()
app.Run();
