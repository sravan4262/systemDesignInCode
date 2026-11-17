# Request flow: single region vs. multi-region sharding

How a request travels from the UI to the right database server, once data is sharded (and optionally split by region). Code: `src/M06.Databases/ShardRouting.cs` (step ①) and `src/M06.Databases/ReadWriteRouting.cs` (step ②).

## 1. Single region (sharded, with replicas)

```
 ┌──────────────┐
 │   Browser    │  user from Tenant 42 (tenant comes from the login token)
 └──────┬───────┘
        │ HTTPS  GET /orders
        ▼
 ┌──────────────────────┐
 │  Load balancer /     │  spreads requests across API instances
 │  API gateway         │  (auth, rate limiting)
 └──────┬───────────────┘
        ▼
 ┌────────────────────────────────────────────────────────────────────┐
 │  API instances (stateless, any one can serve any request)          │
 │                                                                    │
 │  Endpoint  GET /orders  ──▶  TenantOrdersRepository                │
 │                                 │ db.OpenAsync(DbIntent.Read)      │
 │                                 │ (states INTENT only, no server)  │
 │                                 ▼                                  │
 │  ┌──────────────────────────────────────────────────────────────┐  │
 │  │  CONNECTION RESOLUTION  (TenantDbConnectionFactory)          │  │
 │  │                                                              │  │
 │  │  ⓐ Which tenant?                                             │  │
 │  │     ITenantContext reads claim "tenant_id" from the token    │  │
 │  │     → 42            (never from URL/body: could be faked)    │  │
 │  │                          │                                   │  │
 │  │                          ▼                                   │  │
 │  │  ① Which shard?   ShardResolver.ResolveAsync(42)             │  │
 │  │     in-memory cache, fresh (< TTL)? ──yes──▶ "shard-2"       │  │
 │  │            │ no / expired                         ▲          │  │
 │  │            ▼                                      │          │  │
 │  │     ┌──────────────────────┐   42 → shard-2       │          │  │
 │  │     │ Catalog DB           │ ─────────────────────┘          │  │
 │  │     │ TenantShards table   │   (store in cache)              │  │
 │  │     └──────────────────────┘                                 │  │
 │  │     not found → error "tenant not assigned"                  │  │
 │  │                          │                                   │  │
 │  │                          ▼                                   │  │
 │  │  ② Which server inside shard-2?                              │  │
 │  │     intent = Write → Shards["shard-2"]      (PRIMARY)        │  │
 │  │     intent = Read  → Replicas["shard-2"][i] (REPLICA,        │  │
 │  │                      round-robin; none → primary)            │  │
 │  │                          │                                   │  │
 │  │                          ▼                                   │  │
 │  │  ③ new SqlConnection(chosen string).OpenAsync()              │  │
 │  │     (borrowed from the connection pool for that server)      │  │
 │  └──────────────────────────┬───────────────────────────────────┘  │
 │                             │ open connection                      │
 │                             ▼                                      │
 │  Repository runs SQL:  ... WHERE TenantId = @t                     │
 │  (a shard holds MANY tenants, so the filter is still needed)       │
 └─────────────────────────────┬──────────────────────────────────────┘
                               │
        ┌──────────────────────┘
        ▼
 ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
 │   Shard 1    │    │   Shard 2    │    │   Shard 3    │
 │  Primary ◀───┼─W  │  Primary ◀───┼─W  │  Primary     │
 │   │ async    │    │   │ async    │    │   │          │
 │  Replica     │    │  Replica ◀───┼─R  │  Replica     │
 │  Replica     │    │  Replica     │    │  Replica     │
 │ tenants 1,7… │    │ tenants 42…  │    │ tenants 9…   │
 └──────────────┘    └──────────────┘    └──────────────┘
        │                    │                    │
        └────────────────────┼────────────────────┘
                             ▼  (CDC / ETL, not live queries)
                   ┌──────────────────────┐
                   │ Reporting warehouse  │  cross-tenant dashboards
                   └──────────────────────┘
```

## 2. Multi-region (a home region per tenant, then the same picture inside each region)

```
 ┌──────────────┐                          ┌──────────────┐
 │ Browser (DE) │ tenant 42, home = EU     │ Browser (US) │ tenant 77, home = US
 └──────┬───────┘                          └──────┬───────┘
        │ api.app.com                             │ api.app.com
        ▼                                         ▼
 ┌──────────────────────────────────────────────────────────────────┐
 │ GLOBAL ENTRY: Azure Front Door / geo-DNS                        │
 │ → sends each user to the NEAREST region                         │
 └──────┬──────────────────────────────────────────────┬────────────┘
        ▼                                              ▼
 ╔══════════════════════════════╗        ╔══════════════════════════════╗
 ║          EU REGION           ║        ║          US REGION           ║
 ║                              ║        ║                              ║
 ║  API instances               ║        ║  API instances               ║
 ║  ⓪ Home region of tenant?    ║        ║  ⓪ Home region of tenant?    ║
 ║     Global directory (copy)  ║◀──────▶║     Global directory (copy)  ║
 ║     42 → EU, 77 → US         ║ replic.║     42 → EU, 77 → US         ║
 ║                              ║        ║                              ║
 ║   home = EU → continue here  ║        ║   home = US → continue here  ║
 ║   home = US → forward ───────╫───────▶║   (e.g. a US tenant          ║
 ║   (traveller case)           ║        ║    travelling in Europe)     ║
 ║                              ║        ║                              ║
 ║  ① tenant → shard (EU        ║        ║  ① tenant → shard (US        ║
 ║     catalog DB)              ║        ║     catalog DB)              ║
 ║  ② write → primary,          ║        ║  ② write → primary,          ║
 ║     read → replica           ║        ║     read → replica           ║
 ║                              ║        ║                              ║
 ║ ┌────────┐┌────────┐┌──────┐ ║        ║ ┌────────┐┌────────┐┌──────┐ ║
 ║ │EU Sh 1 ││EU Sh 2 ││EU Sh3│ ║        ║ │US Sh 1 ││US Sh 2 ││US Sh3│ ║
 ║ │P + 2 R ││P + 2 R ││P+2 R │ ║        ║ │P + 2 R ││P + 2 R ││P+2 R │ ║
 ║ │        ││ t. 42  ││      │ ║        ║ │ t. 77  ││        ││      │ ║
 ║ └────────┘└────────┘└──────┘ ║        ║ └────────┘└────────┘└──────┘ ║
 ║   EU data NEVER leaves EU    ║        ║                              ║
 ╚══════════════════════════════╝        ╚══════════════════════════════╝
        │                                              │
        └──────────── aggregated / anonymised ─────────┘
                              ▼
                 ┌────────────────────────────┐
                 │ Global reporting warehouse │  (only data allowed to leave a region)
                 └────────────────────────────┘
```

## What each layer decides

| Step | Question | Answered by | Single region | Multi-region |
|---|---|---|---|---|
| ⓪ | Which **region**? | Global entry + global directory (`tenant → home region`) | — | ✅ |
| ① | Which **shard**? | Regional catalog DB (`tenant → shard`), cached | ✅ | ✅ (per region) |
| ② | Which **server**? | Read/write routing (primary vs. replica, read-your-writes) | ✅ | ✅ (per shard) |

## 3. API level: how the connection gets resolved

Full code: `src/M06.Databases/ShardedApi.cs`. The endpoint and repository never mention shards or servers.

```
GET /orders/recent/count   (Authorization: Bearer <token with tenant_id = 42>)
  │
  ├─ Endpoint ............ asks DI for TenantOrdersRepository
  ├─ Repository .......... db.OpenAsync(DbIntent.Read)                 ← states INTENT only
  │    └─ TenantDbConnectionFactory
  │         ⓐ tenant.TenantId           → 42        (from the token's claim, never from URL/body)
  │         ① resolver.ResolveAsync(42) → "shard-2" (cache → catalog DB)
  │         ② Read → shard-2 replica    / Write → shard-2 primary
  │         → new SqlConnection(chosen string).OpenAsync()  (pooled)
  └─ SQL ................. still WHERE TenantId = @t (a shard holds many tenants)
```

```csharp
// The one place that decides the server
public async Task<SqlConnection> OpenAsync(DbIntent intent)
{
    var shardId = await resolver.ResolveAsync(tenant.TenantId);         // ① which shard
    var cs = intent == DbIntent.Write
        ? shards.Shards[shardId]                                         // ② primary
        : PickReplica(shardId) ?? shards.Shards[shardId];                //    or replica
    var conn = new SqlConnection(cs);
    await conn.OpenAsync();
    return conn;
}

// Repository: knows SQL, not servers
await using var conn = await db.OpenAsync(DbIntent.Read);
```

DI lifetimes (Program.cs):

| Registration | Lifetime | Why |
|---|---|---|
| `ShardMapOptions`, `ShardReplicaOptions` | Singleton | Config, read-only |
| `IShardDirectory`, `ShardResolver` | Singleton | Shared, thread-safe, holds the tenant → shard cache |
| `ITenantContext` (from claims) | Scoped | Different user/tenant per request |
| `TenantDbConnectionFactory`, repositories | Scoped | Depend on the current tenant |

Multi-region adds step ⓪ before ①: a global directory gives the tenant's home region; if it isn't this region, forward the request there.

## Key points

- **Each layer answers one question.** Region → residency and latency. Shard → write scale and size. Replica → read scale and availability.
- **Multi-region only adds step ⓪.** Inside a region, everything works exactly like the single-region design.
- **Home region is chosen at sign-up** (country or contract), like shard placement at onboarding. Moving regions is a rare migration project.
- **Cross-shard / cross-region reporting** goes to a warehouse fed by CDC/ETL, never live scatter-gather on production shards.
- **Some data is global** (login, billing, product catalog): replicated to every region or kept in one place.
- **Cost multiplies:** 3 shards × (1 primary + 2 replicas) = 9 servers per region.
- **When it's overkill:** most apps run one region + a disaster-recovery replica. Go multi-region only for residency law, global latency, or regional isolation.
