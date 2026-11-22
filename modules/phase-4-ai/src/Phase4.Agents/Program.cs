using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Phase4.Agents.Domain;
using Phase4.Agents.Support;
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

var app = builder.Build();
app.MapControllers();
app.Run();
