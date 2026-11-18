using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace M15.Observability;

// One trace across:  API (HTTP) → Orders → [queue message] → Billing consumer
// HTTP hops: automatic (instrumentation adds/reads the W3C `traceparent` header).
// Queue hops: YOU (or MassTransit / a broker instrumentation package) must copy the context into message headers.

public static class Telemetry
{
    public static readonly ActivitySource Source = new("Shop.Orders");   // our own spans
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    // PRODUCER: start a "publish" span and write its context into the message headers.
    public static void PublishOrderPlaced(IDictionary<string, object?> headers, Action publish)
    {
        using var span = Source.StartActivity("orders publish", ActivityKind.Producer);
        span?.SetTag("messaging.destination.name", "orders");
        Propagator.Inject(new PropagationContext(span?.Context ?? default, Baggage.Current), headers,
            (h, key, value) => h[key] = value);          // adds headers["traceparent"] = "00-<traceId>-<spanId>-01"
        publish();                                       // e.g. channel.BasicPublish(..., props with these headers)
    }

    // CONSUMER: read the context from the headers and continue the SAME trace.
    public static void HandleOrderPlaced(IDictionary<string, object?> headers, Action handle)
    {
        var parent = Propagator.Extract(default, headers,
            (h, key) => h.TryGetValue(key, out var v) && v is string s ? [s] : []);
        using var span = Source.StartActivity("orders process", ActivityKind.Consumer, parent.ActivityContext);
        handle();                                        // SQL / HTTP calls in here become child spans automatically
    }
    // Outbox note: store the traceparent in the Outbox row too, so the relay's publish
    // continues the original request's trace instead of starting a new one.

    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService("orders-api"))        // service name shown in the trace view
                .WithTracing(t => t
                    .AddAspNetCoreInstrumentation()                        // span per incoming request; reads traceparent
                    .AddHttpClientInstrumentation()                        // span per outgoing call; writes traceparent
                    .AddSource(Source.Name)                                // our publish/process spans
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(0.10))) // head sampling: 10%
                    .AddOtlpExporter());                                   // send to a collector → Jaeger/Tempo/App Insights

            var app = builder.Build();
            app.MapPost("/checkout", () =>
            {
                var headers = new Dictionary<string, object?>();
                PublishOrderPlaced(headers, () => { /* broker publish with headers */ });
                return "placed";
            });
            app.Run();
        }
    }
}
