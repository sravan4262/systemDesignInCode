using Microsoft.Extensions.AI;
using Npgsql;
using OpenAI.Embeddings;
using Phase4.Rag.Ingestion;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

// Key in user-secrets: dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
var apiKey = builder.Configuration["OpenAI:ApiKey"] ?? "missing-key";

// The embedding model turns text into a 1536-number vector. Same model for chunks AND questions.
builder.Services.AddEmbeddingGenerator(new EmbeddingClient("text-embedding-3-small", apiKey).AsIEmbeddingGenerator());

builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Rag")!));
builder.Services.AddSingleton(new Chunker());
builder.Services.AddSingleton<ChunkStore>();

var app = builder.Build();
app.MapControllers();
app.Run();
