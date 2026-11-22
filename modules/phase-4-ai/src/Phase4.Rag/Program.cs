using Microsoft.Extensions.AI;
using Npgsql;
using OpenAI.Embeddings;
using Phase4.Rag.Ingestion;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Key in user-secrets: dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
var apiKey = builder.Configuration["OpenAI:ApiKey"] ?? "missing-key";

// Embedding models turn text into vectors. v1 = current model, v2 = the one we're upgrading to (EmbeddingVersions).
builder.Services.AddKeyedEmbeddingGenerator("v1", new EmbeddingClient("text-embedding-3-small", apiKey).AsIEmbeddingGenerator());
builder.Services.AddKeyedEmbeddingGenerator("v2", new EmbeddingClient("text-embedding-3-large", apiKey).AsIEmbeddingGenerator());
builder.Services.AddSingleton<EmbeddingVersions>();

builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Rag")!));
builder.Services.AddSingleton(new Chunker());
builder.Services.AddSingleton<ChunkStore>();
builder.Services.AddHostedService<EmbeddingBackfill>();   // does nothing unless Embeddings:DualWrite = true

var app = builder.Build();
app.MapControllers();
app.Run();
