using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace M13.Concurrency;

public record Recommendation(string Sku);

// ❌ BEFORE: no timeout (100 s default), no retry rules, no breaker. Recommendations API slow →
//    every product page waits on it → threads/connections pile up → the whole site goes down.
public class RecommendationsClientBefore(HttpClient http)
{
    public Task<List<Recommendation>?> GetAsync(string userId) =>
        http.GetFromJsonAsync<List<Recommendation>>($"/recommendations/{userId}");
}

// ✅ AFTER: the resilience pipeline (Program below) bounds the call; the client adds a FALLBACK.
public class RecommendationsClient(HttpClient http)
{
    public async Task<List<Recommendation>> GetAsync(string userId, CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<List<Recommendation>>($"/recommendations/{userId}", ct) ?? [];
        }
        catch (Exception e) when (e is BrokenCircuitException or TimeoutRejectedException
                                     or HttpRequestException or Polly.RateLimiting.RateLimiterRejectedException)
        {
            return [new("bestseller-1"), new("bestseller-2")];   // FALLBACK: static bestsellers, page still renders
        }
    }

    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddHttpClient<RecommendationsClient>(c => c.BaseAddress = new Uri("https://recs.internal"))
                .AddResilienceHandler("recs", pipeline =>
                {
                    // Order = outermost → innermost. Each request flows through them top to bottom.
                    pipeline.AddTimeout(TimeSpan.FromSeconds(2));                  // 1) TOTAL budget incl. retries

                    pipeline.AddConcurrencyLimiter(new ConcurrencyLimiterOptions   // 2) BULKHEAD: max 20 in flight
                        { PermitLimit = 20, QueueLimit = 10 });                    //    to this dependency; extra → rejected fast

                    pipeline.AddRetry(new HttpRetryStrategyOptions                 // 3) RETRY: transient only (5xx, 408, 429,
                    {                                                              //    HttpRequestException, timeouts)
                        MaxRetryAttempts = 2,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromMilliseconds(200),
                    });

                    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions // 4) BREAKER: ≥50% failures over
                    {                                                                //    10 s (min 20 calls) → OPEN for 30 s
                        FailureRatio = 0.5,
                        SamplingDuration = TimeSpan.FromSeconds(10),
                        MinimumThroughput = 20,
                        BreakDuration = TimeSpan.FromSeconds(30),
                    });

                    pipeline.AddTimeout(TimeSpan.FromMilliseconds(500));           // 5) PER-ATTEMPT timeout (~p99 + margin)
                });

            var app = builder.Build();
            app.MapGet("/products/{id}/page", async (string id, string userId, RecommendationsClient recs, HttpContext ctx) =>
                Results.Ok(new { id, recommendations = await recs.GetAsync(userId, ctx.RequestAborted) }));
            app.Run();
        }
    }
}
// Shortcut: .AddStandardResilienceHandler() gives sensible defaults for all of the above in one line.
// Why the retry sits OUTSIDE the breaker: when the breaker is open, each retry is rejected instantly
// (BrokenCircuitException) instead of hitting the dead service; the outer total timeout caps everything.
