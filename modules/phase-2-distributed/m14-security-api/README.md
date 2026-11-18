# M14 — Security and API design

> Plan: `Senior_Engineer_Interview_Plan.html#m14` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] An authenticated, versioned, idempotent public API with secure webhooks and a short threat model.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
This module covers three questions:
- **Who** is calling? That's authentication.
- **What** are they allowed to do? That's authorization.
- **How are your APIs shaped** so clients can use them safely as they change? That's contracts, pagination, versioning and idempotency.

### Sessions (in order)
1. **OAuth2 and OIDC basics:** tokens and flows; authorization code with PKCE; client credentials.
2. **JWT validation and pitfalls:** signature, algorithm confusion, expiry, revocation, refresh-token rotation, key rotation.
3. **Browser apps:** the BFF pattern; `HttpOnly`/`Secure`/`SameSite` cookies; SPAs as public clients.
4. **Entra ID in practice:** app registrations, scopes, managed identity, `DefaultAzureCredential`, Key Vault.
5. **Authorization:** RBAC and ABAC, policy-based authorization in ASP.NET Core, tenant-aware checks; BOLA. 🧪 Find and fix a BOLA bug.
6. **Service-to-service:** mTLS and secrets management; SAML vs. OIDC (overview).
7. **Threat modelling:** OWASP API Top 10, STRIDE.
8. **API design:** REST vs. gRPC vs. GraphQL, resource models, ProblemDetails, contract-first OpenAPI.
9. **API mechanics:** cursor pagination, versioning, Stripe-style idempotency keys. 🧪 Idempotency middleware with stored responses.

### Done when you can answer
- Why PKCE, and why is the implicit flow dead?
- How do you revoke a JWT before it expires?
- What is BOLA, and how do you prevent it systematically?
- Cursor vs. offset pagination: what's the difference, and when do you use each?

---

## Journal

### Concepts
_One entry per topic, written in your own words after the 7-step concept walkthrough (see CLAUDE.md). Copy the block for each topic._

#### <Topic name>
- **In plain words:**
- **Problem it solves:**
- **Analogy:**
- **Diagram:** (ASCII/Mermaid or a link to `designs/`)
- **Variations / alternatives:**
- **When not to use it:**
- **Quick-check answers:**

#### OAuth2, OIDC, PKCE (Session 1, 2026-11-17)
- **Terms:** OAuth2 = protocol to get an access token for an API without the user's password; OIDC = layer on top that adds login (ID token = who the user is); JWT = token format (header.payload.signature; signed, not encrypted; claims iss/aud/exp/oid/scp/roles); PKCE = proves the app finishing the login is the one that started it.
- **Tokens:** access token → the API (aud, scopes, roles); ID token → the app (name, email); refresh token → only the identity provider.
- **Entra workflow (my words, corrected):** create an app registration for the SPA (client ID, redirect URI, API permission) and one for the API (exposed scope, app roles). SPA redirects to the common `login.microsoftonline.com/{tenant}` page with its client ID; user enters email + password + MFA; Entra returns a CODE (not a token) → SPA exchanges code + verifier at `/token` → ID/access/refresh tokens. The API validates each token LOCALLY with Entra's cached public keys (signature, issuer, audience, expiry, scope/roles); it does not call Entra per request.
- **Per call:** (1) `/authorize` + challenge (hash) → (3) redirect with code → (4) `POST /token` code + verifier → tokens → (5) API with `Bearer`. MSAL.js does PKCE automatically; registering as "SPA" platform makes Entra require it; the API knows nothing about PKCE.
- **PKCE (my words, ~9/10):** SPA creates a secret and passes its HASH at login; in the redirect Entra passes a code; to generate the token both the code and the original secret must be passed; a stolen code is useless even though it's visible in the browser, because you need the code + secret combo. (Initially said the code is "only shared between Entra and the SPA": wrong, the code travels through the browser URL; that's why PKCE exists.)
- **Quick-check answers:** Q1 ✅ ID token = lets the app know who the user is; access token = access to the system and which areas (scopes/roles). Refinement: ID token is for the APP, access token for the API. Q2 ✅ (PKCE line above). Q3 didn't know → client credentials: service logs in as itself (client ID + secret/certificate/managed identity), `scope=api://…/.default`, gets only an access token with `roles` (application permissions, admin-granted), no user/browser/PKCE/refresh token.
- **PKCE does NOT protect a stolen JWT:** access tokens are bearer tokens; defenses = short expiry, HTTPS, keep tokens out of JS-readable storage (BFF), revocation/rotation (Session 2).
- **Entra ID vs. B2B vs. B2C/External ID:** own employees in your tenant; partner staff as guests using their own tenant; public customers in a separate customer tenant (sign-up, social logins). Social login = federation: Google checks the password and returns a signed ID token; the app links its OWN account by Google's `sub` (not email) and issues its OWN session/token.


#### JWT validation and revocation (Session 2, 2026-11-17)
- **API checks (AddJwtBearer):** signature (kid → cached Entra JWKS key) → algorithm pinned (RS256, never none/HS256) → issuer (our tenant) → audience (this API) → lifetime (exp/nbf + skew) → then scp/roles policies. Signature = not tampered; issuer = from our Entra; audience = meant for this API; expiry = still valid.
- **Signature:** no claims inside; = RSA-sign(SHA-256(header.payload), Entra PRIVATE key). API recomputes the hash and verifies with the PUBLIC key. Modified payload → hash changes → signature fails ("tampered", not "malformed" = unparseable). HS256 = shared secret (verifier could forge), so IdPs use RS256.
- **My summary (~9/10):** a token has header, payload, signature; the signature is the fingerprint of the other two. Payload: issuer, audience (which API), permissions (scp), roles, expiry. API checks audience, signature, scp, roles, expiry for validity, authentication and authorization. Fixes: issuer = WHO issued it (IdP + tenant), and the API checks it too; authentication = signature + issuer + audience + expiry, authorization = scp + roles.
- **Key rotation:** unknown `kid` → re-download JWKS; never hard-code keys.
- **Revocation:** access tokens (~60 min) can't be un-signed; revoke refresh tokens at Entra so the next refresh fails; zero window → `jti` denylist in Redis (TTL = remaining life) or CAE. Refresh-token rotation: reuse of an old RT → revoke the whole chain.
- **scp vs. roles (my words):** scp is permissions, roles is roles. Precise: scp = delegated permissions the APP has to act for the signed-in user (consent; space-separated); roles = the caller's app roles (admin-assigned to users/groups), or application permissions for daemons (no scp in client-credentials tokens).
- **Quick-check answers (~3.5/10):** Q1 ❌ said scp cross-verifies; correct: SIGNATURE fails (attacker can't re-sign without Entra's private key). Q2 ⚠️ short-lived tokens ✅; missed that the current token works until expiry, revoked refresh tokens block the next refresh, denylist for zero window. Q3 ❌ said "not malformed"; correct: audience rejects a VALID token issued for another API (replay).


#### BFF + cookies (Session 3, 2026-11-17)
- **Problem:** tokens in browser storage (localStorage, memory) are readable by any script → XSS exfiltrates access + refresh tokens. A stolen token is NOT modified, so it passes every check (signature proves "unchanged", not "who sends it"; bearer = like cash). PKCE doesn't help here.
- **BFF flow (my words, ~9.5/10):** SPA redirects to BFF login → BFF redirects to Microsoft login → user logs in → (Entra returns a CODE to the BFF, which exchanges code + client secret for tokens) → BFF sets the cookie → the browser sends it on every request → BFF reads the cookie, finds the session holding the JWTs, attaches the access token and calls the API. Redirect URI = BFF (`/signin-oidc`), app registration platform = Web (confidential), not SPA. BFF refreshes tokens server-side.
- **Cookie flags:** HttpOnly = page JS can't read it (stops XSS theft); Secure = HTTPS only; SameSite = not sent on cross-site requests (stops CSRF) + custom header like `X-CSRF: 1`. Normal cookies ARE readable via `document.cookie`. HttpOnly cookies live in the browser's cookie store (memory or profile on disk); the user can see them in DevTools, page scripts can't.
- **Limit:** XSS can still make calls through the BFF while the page is open, but can't carry the tokens away. Never put JWTs in query strings (history, logs, Referer).


#### Managed identity (Session 4, 2026-11-17; Key Vault skipped by choice)
- **In plain words:** Azure gives an Azure-hosted app its own Entra identity and issues it tokens with NO secret; the platform vouches for the app (token requested from a local endpoint inside the host).
- **Portal:** enable on the RESOURCE (App Service → Identity → System assigned → On); it appears under Entra → Enterprise applications (Managed Identities), NOT App registrations. The API being called keeps its app registration (Expose an API + App roles, allowed member type Applications). Assign the API's app role to the MI via CLI/Graph `appRoleAssignments` (resourceId = the API's ENTERPRISE APP object id). Azure resources: resource → Access control (IAM); Azure SQL: `CREATE USER [app] FROM EXTERNAL PROVIDER`.
- **Code:** `new DefaultAzureCredential()` → `GetTokenAsync("api://orders-api/.default")`; SQL `Authentication=Active Directory Default`. Credential chain: managed identity in Azure, VS/`az login` locally, env/workload identity in CI.
- **System vs. user-assigned:** tied to one resource vs. standalone, shareable.
- **My words:** we enable managed identity and assign it roles so it can access things (+ code gets tokens via DefaultAzureCredential).
- **Quick-check answers:** Q1 ✅ removes managing client secrets/credentials. Q2 ⚠️ answered with role assignment (authorization); correct: Azure hosts the app and vouches for it via the local token endpoint (authentication). Q3 not answered: DefaultAzureCredential's chain.


#### Authorization + BOLA (Session 5, 2026-11-17)
- **BOLA:** authenticated, but no check that the OBJECT belongs to the caller (`/orders/1002` returns Bob's order to Alice). OWASP API #1.
- **Fix:** ownership in the lookup (`WHERE Id = @id AND OwnerOid = @oid`), caller id ONLY from the validated token's `oid`, return 404 not 403. Systematic: `GetForUser(id, oid)` repositories / EF global query filters on OwnerId/TenantId, a "user A asks for B's resource → 404" test per endpoint; GUIDs = defense in depth, not the fix. RBAC (role) vs. ABAC (attributes of user + object; resource-based authorization).
- **Code:** `src/M14.Security/Bola.cs`.
- **Quick-check answers (~5/10):** Q1 ✅ missing ownership check. Q2 ❌ said the token's user id can be manipulated: reversed; the token can't be changed (signature), the query string can. Q3 ⚠️ 404 because it isn't theirs ✅; missing reason: 403 confirms the object exists → enumeration leaks data.


#### API design (Session 6, high level, 2026-11-17)
- **REST vs. gRPC vs. GraphQL:** REST for public/general APIs (simple, cacheable); gRPC for internal service-to-service at high volume (Protobuf binary, HTTP/2 multiplexing, generated typed clients, strict `.proto` contract); GraphQL for frontends needing different shapes (no over-fetching; hard to cache, needs query cost limits).
- **REST basics:** plural nouns, correct verbs (PUT/DELETE idempotent), 201 + Location, action sub-resources (`/orders/42/cancel`), correct status codes (401 vs. 403, 409, 422, 429).
- **ProblemDetails (RFC 9457):** one error shape (type, title, status, detail, instance, traceId); `AddProblemDetails()` + `Results.Problem`; never leak stack traces/SQL.
- **Contract-first:** OpenAPI / `.proto` agreed before code, generate clients and stubs.
- **Quick-check answers (~6.5/10):** Q1 gRPC ✅, but reasons reversed: it has a STRICTER contract and runs ON HTTP/2; real reasons = binary Protobuf, HTTP/2 multiplexing, generated clients. Q2 ✅ consistent format across APIs; refine "no business logic leaked" → no internals (stack traces, SQL) + traceId for support.


#### API mechanics (Session 7, 2026-11-17)
- **Pagination:** offset = slow at depth (reads + discards) and drifts on inserts; cursor/keyset = `WHERE (CreatedAt, Id) < (@c, @id) ORDER BY … TOP N` with an index → same speed on every page, stable; opaque base64 cursor; no jump-to-page. Offset for small admin tables, cursor for feeds/APIs/large tables.
- **Versioning:** additive changes need no version (tolerant reader); breaking changes → `/v2`, run side by side, `Sunset` header, retire v1.
- **Idempotency keys:** client sends one key per operation on every retry; PK on (oid:key) is the referee; finished → replay stored response; in progress → 409; different body → 422; 5xx → release (Stripe instead replays even 5xx); 24 h TTL; strongest = store the response in the same tx as the business change. Code: `src/M14.Security/IdempotencyMiddleware.cs`.
- **Stripe:** your app generates the key (deterministic `order-{id}-charge` or a GUID saved once; never a new GUID per attempt); Stripe stores key → first result for 24 h per account and replays it (success, decline, 409 if concurrent, error if params differ). New card = new key. Store `pi_…` as the permanent payment id.
- **Quick-check answers:** Q3 ✅ breaking change to request/response contracts → new version (add: additive changes don't). Q2 ⚠️ said the second waits then is ignored; correct: 409 immediately while in progress, then the stored response is REPLAYED. Q1 (cursor vs. offset) not answered → CP2.


### Problem
_What real engineering problem does this concept solve?_

### Internals
_How does it work? What guarantees does it provide?_

### Implementation
_What did I build? Code paths, schema, configuration, tests._

### Failure
_What did I deliberately break? What happened? (Tick the matching items in the plan's section 4.)_

### Recovery
_How does the system detect, recover, or reconcile the failure?_

### Trade-offs
_What alternatives exist? Why choose this design?_

### Production
_What should be monitored? What are the operational risks?_

### Interview
_Follow-up questions an interviewer could ask, with my short answers._

### My notes
_Which Apple Notes did this update or correct?_
