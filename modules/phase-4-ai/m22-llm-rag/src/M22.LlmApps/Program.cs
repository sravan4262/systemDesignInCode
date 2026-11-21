using Microsoft.Extensions.AI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// API key: dotnet user-secrets set OpenAI:ApiKey sk-...   (never in appsettings / git)
var apiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("Set OpenAI:ApiKey with dotnet user-secrets.");
var model = builder.Configuration["OpenAI:Model"] ?? "gpt-4.1-mini";

builder.Services.AddControllers();
builder.Services
    .AddChatClient(new ChatClient(model, apiKey).AsIChatClient())          // the real OpenAI API
    .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 5); // runs the tool loop, max 5 rounds

var app = builder.Build();
app.MapControllers();
app.Run();
