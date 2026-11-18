# Amazon-style shop on Azure: from typing the URL to the response

Reference for M16 Session 3 (building blocks). Read top to bottom: domain → DNS → edge → region → services → data.
Style: one box = one component; branches (├──► / └──►) where the flow splits.

## Key words

| Term | Plain meaning |
|---|---|
| **Registrar** | Where you rented the domain (GoDaddy). Tells the `.com` servers who holds your DNS records |
| **Authoritative DNS** | The DNS server that **owns** a domain's records, so its answer is the original (GoDaddy DNS or Azure DNS) |
| **Resolver** | The DNS server the user's device asks (ISP, `8.8.8.8`). Does the lookup and **caches** the answer for its **TTL** |
| **CNAME** | "This name is really that other name: go look that one up" |
| **Edge** | Front Door / CDN servers in hundreds of cities, **near users**. Configured, not deployed to |
| **Origin** | Your own servers and storage behind the edge |

---

## 1. DNS: domain bought at GoDaddy, app on Azure

Azure never calls GoDaddy. Everything connects through public DNS.

### Journey A: DNS records stay at GoDaddy

**Setup (once)**
```
┌────────────────────────────────────┐
│ GoDaddy DNS panel                   │
│  www       CNAME → myshop.azurefd.net│
│  _dnsauth  TXT   → 7f3k9…            │
└─────────────────┬──────────────────┘
                  ▼
┌────────────────────────────────────┐
│ Azure Front Door                    │
│  reads the TXT via public DNS       │
└─────────────────┬──────────────────┘
                  ▼
┌────────────────────────────────────┐
│ Domain verified                     │
│  → TLS certificate issued           │
└────────────────────────────────────┘
```

**Every lookup (user in Sydney types www.myshop.com)**
```
┌──────────────────────────┐
│ ① Browser                 │  "IP of www.myshop.com?"
└────────────┬─────────────┘
             ▼
┌──────────────────────────┐
│ ② Resolver (ISP/8.8.8.8)  │
└────────────┬─────────────┘
             ├──► cached → return IP to browser (done)
             │
             └──► not cached
                     ▼
             ┌──────────────────────────┐
             │ ③ Root servers            │  "for .com, ask the .com servers"
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ④ .com servers (Verisign) │  "myshop.com's records are at GoDaddy DNS"
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ⑤ GoDaddy DNS             │  AUTHORITATIVE for myshop.com
             │                           │  "www = CNAME myshop.azurefd.net"
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ⑥ Azure DNS               │  AUTHORITATIVE for azurefd.net
             │                           │  "= 13.107.x.x" (Sydney EDGE, not the app)
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ⑦ Resolver                │  caches each answer for its TTL
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ⑧ Browser                 │  connects to 13.107.x.x (the Sydney edge)
             └──────────────────────────┘
```
Gotcha: the bare domain `myshop.com` can't be a CNAME → GoDaddy forwarding to `www`.

### Journey B: DNS records moved to Azure DNS (GoDaddy is only the registrar)

**Setup (once)**
```
┌────────────────────────────────────┐
│ Azure DNS                           │
│  create zone myshop.com             │
│  → 4 nameservers                    │
│    (ns1-03.azure-dns.com …)         │
└─────────────────┬──────────────────┘
                  ▼
┌────────────────────────────────────┐
│ GoDaddy (registrar settings)        │
│  nameservers → the 4 Azure ones     │
└─────────────────┬──────────────────┘
                  ▼
┌────────────────────────────────────┐
│ .com servers                        │
│  now say "ask Azure DNS"            │
└─────────────────┬──────────────────┘
                  ▼
┌────────────────────────────────────┐
│ Azure DNS records                   │
│  myshop.com  ALIAS → Front Door     │
│  www         CNAME → …azurefd.net   │
│  TXT → verified → TLS certificate   │
└────────────────────────────────────┘
```

**Every lookup**
```
┌──────────────────────────┐
│ ① Browser                 │
└────────────┬─────────────┘
             ▼
┌──────────────────────────┐
│ ② Resolver                │
└────────────┬─────────────┘
             ├──► cached → return IP (done)
             │
             └──► not cached
                     ▼
             ┌──────────────────────────┐
             │ ③ Root servers            │  "ask the .com servers"
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ④ .com servers            │  "myshop.com's records are at Azure DNS"
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ⑤ Azure DNS               │  AUTHORITATIVE for myshop.com
             │                           │  "ALIAS → Front Door" → Sydney edge IP
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ⑥ Resolver                │  caches for TTL
             └────────────┬─────────────┘
                          ▼
             ┌──────────────────────────┐
             │ ⑦ Browser                 │  connects to the Sydney edge
             └──────────────────────────┘
```

| | Journey A | Journey B |
|---|---|---|
| Records edited in | GoDaddy | Azure (portal / Terraform) |
| Bare domain → Front Door | Forwarding only | ALIAS record |
| GoDaddy's role | Registrar + DNS | Registrar only |

### DNS gives the EDGE's IP. How does the edge find your app?

Many customers share the same edge IPs. The edge tells them apart by the domain name in the request.

```
┌──────────────────────────────┐
│ Browser                       │  TLS: "I want www.myshop.com" (SNI)
│                               │  Host: www.myshop.com
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ Sydney edge                   │  finds YOUR config by domain → your TLS cert
└──────────────┬───────────────┘
               ├──► /static/*  ──►┌──────────────────────────┐
               │                  │ Blob Storage (origin)     │
               │                  └──────────────────────────┘
               │
               └──► /api/*     ──►┌──────────────────────────┐
                                  │ Origin group "api"        │  health probes every few s
                                  └────────────┬─────────────┘
                                               ├──► ┌──────────────────────────────┐
                                               │    │ myshop-eus.azurewebsites.net  │ ✅ nearest
                                               │    └──────────────────────────────┘
                                               │
                                               └──► ┌──────────────────────────────┐
                                                    │ myshop-weu.azurewebsites.net  │ ✅ backup
                                                    └──────────────────────────────┘
```
The edge itself resolves `myshop-eus.azurewebsites.net` → the app's IP. Only the edge sees it.

| Who does the lookup | Name | Gets |
|---|---|---|
| User's browser | `www.myshop.com` → CNAME → `…azurefd.net` | Edge IP |
| The edge | `myshop-eus.azurewebsites.net` | App's IP |

### What the CNAME points at

The CNAME target is the default host name of the resource users should hit **first**.

| Resource | Default host name (CNAME target) |
|---|---|
| App Service | `myshop.azurewebsites.net` |
| Container Apps | `myapi.<env>.eastus.azurecontainerapps.io` |
| Front Door | `myshop-abc123.z01.azurefd.net` |
| AKS | no host name: ingress public IP → A record |

```
┌──────────────────────────────┐
│ DNS: www.myshop.com           │
└──────────────┬───────────────┘
               ├──► no Front Door ──► CNAME myshop.azurewebsites.net
               │                         ▼
               │               ┌──────────────────────────┐
               │               │ App Service               │  custom domain + TLS here
               │               └──────────────────────────┘
               │
               └──► Front Door ─────► CNAME myshop-abc123.z01.azurefd.net
                                         ▼
                               ┌──────────────────────────┐
                               │ Front Door                │  custom domain + TLS here
                               └────────────┬─────────────┘
                                            ▼
                               ┌──────────────────────────┐
                               │ App Service (origin)      │  accepts Front Door traffic only
                               └──────────────────────────┘
```

---

## 2. Deployment: what goes where on Azure

```
┌──────────────────────────────┐
│ GLOBAL (configured once)      │
└──────────────┬───────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Azure DNS                 │  domain → Front Door
               │    └──────────────────────────┘
               │
               └──► ┌──────────────────────────┐
                    │ Azure Front Door          │  certs · WAF · caching · routes · probes
                    └────────────┬─────────────┘
                                 ├──► ┌──────────────────────┐
                                 │    │ REGION: East US       │
                                 │    └──────────────────────┘
                                 │
                                 └──► ┌──────────────────────┐
                                      │ REGION: West Europe   │  (same stack)
                                      └──────────────────────┘
```

**Inside each region**
```
┌──────────────────────────────┐
│ API Management                │  auth (JWT), rate limit, route
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ AKS (or Container Apps)       │  ingress = load balancer
└──────────────┬───────────────┘
               ├──► ┌────────────────┐
               │    │ catalog         │
               │    └────────────────┘
               ├──► ┌────────────────┐
               │    │ search          │
               │    └────────────────┘
               ├──► ┌────────────────┐
               │    │ cart            │
               │    └────────────────┘
               ├──► ┌────────────────┐
               │    │ orders          │
               │    └────────────────┘
               └──► ┌────────────────┐
                    │ workers         │  payment · inventory · notification
                    └────────────────┘

┌──────────────────────────────┐
│ Data + messaging the services │
│ use (same region)             │
└──────────────┬───────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Azure Cache for Redis     │  hot products, sessions
               │    └──────────────────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Azure SQL                 │  orders, payments, outbox (geo-replica)
               │    └──────────────────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Cosmos DB                 │  catalog, carts (multi-region)
               │    └──────────────────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Azure AI Search           │  product search
               │    └──────────────────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Service Bus / Event Hubs  │  OrderPlaced → workers
               │    └──────────────────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Blob Storage              │  images, static files
               │    └──────────────────────────┘
               └──► ┌──────────────────────────┐
                    │ Key Vault · App Config ·  │  secrets, settings, telemetry
                    │ App Insights              │
                    └──────────────────────────┘
```

**What a deploy does**
```
┌──────────────────────────────┐
│ build + test                  │
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ Container Registry            │  images tagged with commit SHA
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ Blob Storage                  │  static files with versioned names
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ AKS East US                   │  canary 5% → 100%
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ AKS West Europe               │
└──────────────────────────────┘
Front Door: nothing to deploy. Edges fetch new static files on the next request.
```

---

## 3. User journey: open a product page

```
┌──────────────────────────────┐
│ ① DNS (section 1)             │  → Sydney edge IP
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ ② Sydney edge                 │  TCP + TLS here (~10 ms away), WAF check
└──────────────┬───────────────┘
               ├──► static file cached → return it (done)
               │
               └──► API call → warm connection to the region
                       ▼
               ┌──────────────────────────────┐
               │ ③ API Management (East US)    │  rate limit, route /products
               └──────────────┬───────────────┘
                              ▼
               ┌──────────────────────────────┐
               │ ④ AKS ingress                 │  pick a healthy catalog pod
               └──────────────┬───────────────┘
                              ▼
               ┌──────────────────────────────┐
               │ ⑤ Catalog service             │
               └──────────────┬───────────────┘
                              ▼
               ┌──────────────────────────────┐
               │ ⑥ Redis                       │
               └──────────────┬───────────────┘
                              ├──► HIT → return product (done)
                              │
                              └──► MISS
                                      ▼
                              ┌──────────────────────────────┐
                              │ ⑦ Cosmos DB                   │  read → write to Redis (TTL) → return
                              └──────────────────────────────┘
Response: catalog → APIM → Sydney edge → browser. Image URLs point at the edge.
```

## 4. User journey: checkout

```
┌──────────────────────────────┐
│ ① Sydney edge                 │
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ ② API Management              │  JWT required; user id from the token
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ ③ Orders service              │  POST /orders + Idempotency-Key
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ ④ Azure SQL (one transaction) │  INSERT order (Pending)
│                               │  claim stock (atomic conditional UPDATE)
│                               │  INSERT outbox row (OrderPlaced)
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ ⑤ 202 Accepted → user         │  no payment call in the request
└──────────────────────────────┘

        ── later, in the background ──

┌──────────────────────────────┐
│ ⑥ Outbox relay                │  reads unsent rows
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ ⑦ Service Bus                 │  OrderPlaced
└──────────────┬───────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Payment worker            │  charge with idempotency key
               │    └──────────────────────────┘
               ├──► ┌──────────────────────────┐
               │    │ Inventory worker          │  confirm reservation
               │    └──────────────────────────┘
               └──► ┌──────────────────────────┐
                    │ Notification worker       │  email + push
                    └──────────────────────────┘
Payment fails → Saga compensation: release stock, cancel order.
```

## 5. Which block solves which problem

| Problem | Block |
|---|---|
| Users far away | DNS + edge (Front Door) |
| Attacks, bots | WAF at the edge |
| Auth, rate limits, routing to services | API Management |
| More load than one server, servers dying | Load balancer + stateless services (AKS) |
| Hot reads | Redis |
| Transactions | Azure SQL |
| Search | Azure AI Search |
| Images, static files | Blob Storage + edge cache |
| Slow or fragile steps (payment, email) | Outbox → Service Bus → workers |
