using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Phase4.ProductionAi.Guardrails;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));   // "Block", not 3

// Key in user-secrets: dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
var apiKey = builder.Configuration["OpenAI:ApiKey"] ?? "missing-key";
builder.Services.AddChatClient(new ChatClient(builder.Configuration["OpenAI:Model"] ?? "gpt-4.1-mini", apiKey).AsIChatClient());

// ---------- Guardrails: two versions of each guard ----------
var thresholds = builder.Configuration.GetSection("Guardrails").Get<GuardThresholds>() ?? new GuardThresholds();

// In code
builder.Services.AddSingleton<RegexPiiRedactor>();
builder.Services.AddSingleton<KeywordModerator>();

// By calling an endpoint (Azure AI Language, Azure AI Content Safety). Typed HttpClients: base address + key header,
// a SHORT timeout (a guard on every request must not add seconds), and the standard retry/circuit-breaker handler.
builder.Services.AddHttpClient<AzurePiiRedactor>(c => ConfigureAzure(c, "Azure:Language")).AddStandardResilienceHandler();
builder.Services.AddHttpClient<AzureContentSafetyModerator>(c => ConfigureAzure(c, "Azure:ContentSafety")).AddStandardResilienceHandler();

// Keyed: same type, two registrations. The controller picks one by the {mode} in the URL.
builder.Services.AddKeyedTransient("code", (sp, _) => new GuardrailPipeline(
    sp.GetRequiredService<RegexPiiRedactor>(), sp.GetRequiredService<KeywordModerator>(), thresholds,
    sp.GetRequiredService<ILogger<GuardrailPipeline>>()));

builder.Services.AddKeyedTransient("service", (sp, _) => new GuardrailPipeline(
    sp.GetRequiredService<AzurePiiRedactor>(), sp.GetRequiredService<AzureContentSafetyModerator>(), thresholds,
    sp.GetRequiredService<ILogger<GuardrailPipeline>>(),
    fallbackModerator: sp.GetRequiredService<KeywordModerator>()));   // service down → in-code check, not no check

var app = builder.Build();
app.MapControllers();
app.Run();

void ConfigureAzure(HttpClient client, string section)
{
    client.BaseAddress = new Uri(builder.Configuration[$"{section}:Endpoint"] ?? "https://missing.cognitiveservices.azure.com/");
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", builder.Configuration[$"{section}:Key"] ?? "missing-key");
    client.Timeout = TimeSpan.FromSeconds(3);
}
