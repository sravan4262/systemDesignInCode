# Request path on Azure: Amazon-style shop, two regions

Front Door sends static files to the CDN cache, and each API call to ONE region: the nearest healthy one.
Region 1 hosts the services on **AKS**; Region 2 hosts the same services on **Container Apps** (no AKS).

```
                                ┌────────────────────────────────┐
                                │ User (browser / app)           │
                                └────────────────┬───────────────┘
                                                 ▼
                                ┌────────────────────────────────┐
                                │ Azure DNS                      │
                                │ shop.com → nearest edge IP     │
                                └────────────────┬───────────────┘
                                                 ▼
                                ┌────────────────────────────────┐
                                │ Front Door (edge)              │
                                │ TCP + TLS handshake            │
                                │ WAF · CDN · health probes      │
                                └────────────────┬───────────────┘
            ┌────────────────────────────────────┼─────────────────────────────────────┐
            ▼                                    ▼                                     ▼
┌──────────────────────┐          ┌────────────────────────────┐        ┌────────────────────────────┐
│ /static/*  (CDN)     │          │ /api/* → REGION 1          │        │ /api/* → REGION 2          │
│ images · JS · CSS    │          │ East US · AKS              │        │ West Europe · no AKS       │
└───────────┬──────────┘          └──────────────┬─────────────┘        └──────────────┬─────────────┘
            ▼                                    ▼                                     ▼
┌──────────────────────┐          ┌────────────────────────────┐        ┌────────────────────────────┐
│ Edge cache           │          │ API Management             │        │ API Management             │
│ HIT  → return it     │          │ API gateway:               │        │ API gateway:               │
│ MISS → Blob Storage, │          │ JWT · rate limit · routing │        │ JWT · rate limit · routing │
│        then cache it │          │                            │        │                            │
└──────────────────────┘          └──────────────┬─────────────┘        └──────────────┬─────────────┘
                                                 ▼                                     ▼
                                  ┌────────────────────────────┐        ┌────────────────────────────┐
                                  │ Application Gateway        │        │ Container Apps ingress     │
                                  │ load balancer (L7)         │        │ built-in load balancer     │
                                  └──────────────┬─────────────┘        └──────────────┬─────────────┘
                                                 ▼                                     ▼
                                  ┌────────────────────────────┐        ┌────────────────────────────┐
                                  │ AKS pods                   │        │ Container Apps             │
                                  │ catalog · cart · orders ·  │        │ catalog · cart · orders ·  │
                                  │ search · workers           │        │ search · workers           │
                                  └──────────────┬─────────────┘        └──────────────┬─────────────┘
                                                 └──────────────────┬──────────────────┘
                                                                    ▼
                                                  ┌──────────────────────────────────┐
                                                  │ Data layer (a copy per region)   │
                                                  └─────────────────┬────────────────┘
            ┌───────────────────────┬───────────────────────┬───────┴───────────────┬───────────────────────┐
            ▼                       ▼                       ▼                       ▼                       ▼
  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
  │ Redis            │    │ Cosmos DB        │    │ Azure SQL        │    │ AI Search        │    │ Service Bus      │
  │ hot products     │    │ catalog, carts   │    │ orders + outbox  │    │ product search   │    │ OrderPlaced →    │
  │                  │    │                  │    │                  │    │                  │    │ workers          │
  └──────────────────┘    └──────────────────┘    └──────────────────┘    └──────────────────┘    └──────────────────┘
```

## The two regions side by side

| Step | Region 1: East US (AKS) | Region 2: West Europe (no AKS) |
|---|---|---|
| API gateway | API Management | API Management |
| Load balancer | Application Gateway | Container Apps built-in ingress |
| Runs the code | AKS pods (you manage the cluster) | Container Apps (Azure manages it) |
| Pick it when | Many services, need full Kubernetes control | Simpler ops, same containers, no cluster to run |

## What each block is for

| Block | Azure service | Job |
|---|---|---|
| DNS | Azure DNS | Name → nearest edge IP |
| Edge + CDN + WAF | Front Door | TLS near the user, cache static files, block attacks, pick a healthy region |
| Static origin | Blob Storage | Images, JS, CSS |
| API gateway | API Management | Auth (JWT), rate limits, route `/products`, `/cart`, `/orders` |
| Load balancer | Application Gateway / Container Apps ingress | Spread requests over healthy instances |
| Services | AKS or Container Apps | Stateless app code |
| Cache | Azure Cache for Redis | Hot reads |
| Catalog, carts | Cosmos DB | Multi-region data |
| Orders | Azure SQL | Transactions + outbox (geo-replica in the other region) |
| Search | Azure AI Search | Full-text search |
| Messaging | Service Bus | OrderPlaced → payment / inventory / notification workers |
