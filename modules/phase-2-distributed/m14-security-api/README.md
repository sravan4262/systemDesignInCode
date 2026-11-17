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
