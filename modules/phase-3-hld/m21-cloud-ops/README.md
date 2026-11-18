# M21 — Advanced architecture and cloud operations

> Plan: `Senior_Engineer_Interview_Plan.html#m21` · Status lives in `/PROGRESS.md`

## Exit deliverable
- [ ] A multi-region SaaS design with tenant isolation, regional data strategy, DR, and a deployment plan.

## Before starting
- [ ] Reviewed the Apple Notes listed in the plan's 🟧 box for this module
- [ ] Applied any corrections from plan section 10 to those notes

## Layout
- `src/`: code for this module (add projects to the root solution; tests only when asked)
- `designs/`: diagrams and write-ups (HLD modules)
- This README: the journal

## Learning plan

### Big picture (read first)
Running your designs **in production**. This module covers:
- surviving a region outage
- isolating tenants
- deploying on Kubernetes and Azure compute
- shipping changes safely
- mapping all of it to AWS

### Sessions (in order)
1. **Multi-region:** active-active vs. active-passive, RTO and RPO, failover, replication lag, conflict resolution.
2. **Multi-tenancy:** isolation models (shared, schema per tenant, DB per tenant), quotas, noisy neighbours.
3. **Containers:** multi-stage Dockerfiles, non-root, port 8080, image tags by commit SHA.
4. **Kubernetes:** Deployments vs. StatefulSets, Services, ingress, probes, HPA/VPA/cluster autoscaler, ConfigMaps and Secrets, Helm.
5. **Azure compute:** App Service vs. Container Apps vs. AKS vs. Functions (isolated worker model, Durable Functions).
6. **Safe delivery:** blue-green, canary, rollback, backward-compatible schemas.
7. **Azure ↔ AWS mapping;** Terraform with remote state. 🧪 Deploy one capstone service.
8. **Trade-offs:** cost, compliance, operational complexity.

### Done when you can answer
- RTO vs. RPO: what are they, and what drives each one?
- Active-active: how do you handle write conflicts?
- When do you choose Container Apps vs. AKS vs. Functions?
- How do you roll out a breaking schema change with zero downtime?

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

#### Multi-region, multi-tenancy, containers, Kubernetes (S1–S4)
- **Multi-region:** zones for a datacenter failure, regions for a region outage; active-passive (SQL failover group, RPO seconds, RTO minutes) by default; active-active (Cosmos multi-region writes) for global low latency, home region per user to avoid conflicts.
- **Multi-tenancy:** shared tables + query filter + RLS for small tenants, DB per tenant (Elastic Pool) for big/regulated, tenant catalog to route; tenant ID from the JWT; noisy neighbours → per-tenant rate limits, own DB, reports on a read replica.
- **Read-heavy ladder:** CDN → Redis → read replica (watch replication lag, read-your-writes on primary) → analytics store.
- **Containers:** image = bundle, container = running instance; multi-stage, non-root, port 8080, SHA tags (traceability + exact rollback).
- **Kubernetes:** orchestrator. Container (app) in a pod (wrapper, IP) on a node (VM); Deployment keeps N pods + rolling updates; Service = stable name + internal LB; Ingress = external L7 entry. Liveness → restart (app only); readiness → out of rotation (deps). HPA pods, cluster autoscaler nodes, KEDA on queue length.
- **Drill:** 114/180; gaps logged in PROGRESS.
- **Azure compute:** Functions (event glue, pay per run; Durable = workflows), App Service (simplest web hosting on managed VMs), Container Apps (microservices, KEDA, scale to zero), AKS (full control, most ops).
- **Safe delivery:** rolling / blue-green (full copy, switch) / canary (small v2, weights at the routing layer moved in stages, instant rollback); expand/contract because v1 and v2 share the DB; feature flags separate deploy from release.
- **Helm:** templated Kubernetes YAML + values per environment, installed as one versioned release (upgrade/rollback); the pipeline calls Helm, Kubernetes orchestrates.
- **Trade-offs:** cost, compliance (residency, PII), ops; end designs with triggers for when to change. S7 (AWS mapping, Terraform) skipped.
