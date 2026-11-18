# Capacity per layer on Azure: sizing a 100,000 req/s peak

Top to bottom, the same path as `request-path-azure.md`. Each box: the layer, its rough capacity per unit, and the sizing.
Left column = load that STOPS early (edge cache, Redis hits). Middle = the main path. Right = files and async work.
Numbers are rough starting points for interview estimates; confirm with a load test.

## Capacity checklist (in order)
```
① Convert:   per day ÷ 10⁵ = per second → × 2–3 for peak
② API:       req/s per service (reads + writes) ÷ (≈500–1,000 per core × cores × 70%)
             → servers; min 2–3; autoscale
③ Files:     requests/s × file size = bandwidth → CDN, resize images
④ Cache:     memory = items × size × 1.5 → shards; check ops/s; replica per shard
             → apply the hit rate before ⑤
⑤ Database:  reads that MISS the cache + all writes
             reads ≈ 1–2K/s per vCore (data in RAM); writes ≈ log speed (few K/s)
             too many reads → replicas · too many writes / too much data → shard
             size: ~4 TB on most tiers, ~100 TB on Hyperscale
⑥ Storage:   per day × retention → which store (Blob for files, SQL/Cosmos for rows)
⑦ Say "so…": what each number changes in the design
```

```
                                        ┌──────────────────────────────────┐
                                        │ PEAK TRAFFIC                     │
                                        │ 100,000 req/s                    │
                                        └─────────────────┬────────────────┘
                                                          ▼
                                        ┌──────────────────────────────────┐
                                        │ Front Door (edge)                │
                                        │ capacity: effectively unlimited  │
                                        │ role: absorb + cache             │
                                        └─────────────────┬────────────────┘
               ┌──────────────────────────────────────────┼──────────────────────────────────────────────┐
               ▼                                          ▼                                              ▼
┌────────────────────────────┐          ┌──────────────────────────────────┐            ┌────────────────────────────────┐
│ Served AT THE EDGE         │          │ To the region                    │            │ Uploads / downloads            │
│ 80% static / cacheable     │          │ 20% = 20,000 req/s               │            │ (files go direct, via SAS)     │
│ = 80,000 req/s             │          │                                  │            │                                │
│ origin never sees them     │          │                                  │            │                                │
└────────────────────────────┘          └─────────────────┬────────────────┘            └────────────────┬───────────────┘
                                                          ▼                                              ▼
                                        ┌──────────────────────────────────┐            ┌────────────────────────────────┐
                                        │ API Management (gateway)         │            │ Blob Storage                   │
                                        │ ~2,500 req/s per unit            │            │ ~20,000+ req/s per account     │
                                        │ 20,000 ÷ 2,500 ≈ 8 units         │            │ big files never touch the API  │
                                        └─────────────────┬────────────────┘            └────────────────────────────────┘
                                                          ▼
                                        ┌──────────────────────────────────┐
                                        │ Application Gateway (L7 LB)      │
                                        │ autoscales: thousands to         │
                                        │ tens of thousands req/s          │
                                        └─────────────────┬────────────────┘
                                                          ▼
                                        ┌──────────────────────────────────┐
                                        │ API servers (2 vCPU each)        │
                                        │ ~1,500 req/s per instance        │
                                        │ 20,000 ÷ 1,500 ≈ 14              │
                                        │ + headroom → ~20 instances       │
                                        └─────────────────┬────────────────┘
                                                          ▼
                                        ┌──────────────────────────────────┐
                                        │ Redis                            │
                                        │ ~100,000 ops/s per node          │
                                        │ 20,000 ops/s → 1 node OK         │
                                        └─────────────────┬────────────────┘
               ┌──────────────────────────────────────────┼──────────────────────────────────────────────┐
               ▼                                          ▼                                              ▼
┌────────────────────────────┐          ┌──────────────────────────────────┐            ┌────────────────────────────────┐
│ Cache HIT (90%)            │          │ Cache MISS (10%)                 │            │ Writes → async work            │
│ = 18,000 req/s             │          │ = 2,000 req/s to the DB          │            │ OrderPlaced events             │
│ returned from Redis        │          │                                  │            │                                │
│ DB never sees them         │          │                                  │            │                                │
└────────────────────────────┘          └─────────────────┬────────────────┘            └────────────────┬───────────────┘
                                                          ▼                                              ▼
                                        ┌──────────────────────────────────┐            ┌────────────────────────────────┐
                                        │ Azure SQL  or  Cosmos DB         │            │ Service Bus / Event Hubs       │
                                        │ SQL: 2,000 reads/s → one         │            │ Event Hubs: 1 unit =           │
                                        │   mid-size server (in RAM)       │            │   1,000 events/s in            │
                                        │ Cosmos: 2,000 × 1 RU             │            │ Service Bus: thousands         │
                                        │   ≈ 2,000 RU/s                   │            │   msg/s per unit               │
                                        └──────────────────────────────────┘            └────────────────────────────────┘
```

## The lesson
Each cache cuts the load on the next layer: the edge takes 80% before the region, Redis takes 90% before the database.
100,000 req/s at the top becomes 2,000 req/s at the database.

## Capacity per layer (reference)

| Layer (Azure) | Unit you size | Rough throughput | What limits it | How to scale |
|---|---|---|---|---|
| Front Door | — | Effectively unlimited | Your origin behind it | Cache more |
| API Management | Scale units | ~1,000–4,000 req/s per unit | Policy work | Add units |
| Application Gateway | Autoscale instances | Thousands–tens of thousands req/s | New TLS connections | Autoscaling |
| API servers | Instance (2 vCPU) | ~500–2,000 req/s | CPU per request | Add instances (stateless) |
| Redis | Node / shard | ~100,000 ops/s per node | Network, RAM (data × ~1.5) | Bigger tier, clustering |
| Azure SQL | vCores + RAM | Reads in RAM ~5,000–20,000/s; small writes ~2,000–10,000/s | Hot data in RAM; log write speed | Scale up, read replicas, sharding |
| Cosmos DB | RU/s | 1 KB read ≈ 1 RU, write ≈ 5 RU | RU/s bought; hot partition key | More RU/s, good partition key |
| AI Search | Replicas × partitions | Tens–hundreds of queries/s per replica | Query complexity | Replicas (QPS), partitions (size) |
| Service Bus | Messaging units | Thousands of msg/s per unit | Message size, sessions | More units / queues |
| Event Hubs | Throughput units | 1 unit = 1 MB/s or 1,000 events/s in | Units; partition count | More units, enough partitions |
| Blob Storage | Storage account | ~20,000+ req/s per account | Request rate | Front Door caching, more accounts |

## Estimating one instance yourself
```
req/s per core ≈ 1000 ms ÷ CPU ms per request      (2 ms → 500 req/s per core)
plan for 60–70% CPU; in-flight requests = req/s × latency (Little's Law)
```
