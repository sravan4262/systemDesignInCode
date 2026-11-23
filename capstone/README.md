# Capstone: reliable distributed order platform

One evolving system that connects every concept in the plan. Started in M6, extended in each module after, M28 (integrate, harden, publish) was removed by the learner on 2026-11-23, so the capstone stays as it is.

See the plan's section 6 for components, the core order workflow, and the exit criterion.

## Layout
- `src/`: services (`Capstone.OrderService`, `Capstone.InventoryService`, `Capstone.PaymentService`, `Capstone.NotificationService`, `Capstone.SagaCoordinator`, …)
- `docs/adr/`: Architecture Decision Records (`0001-use-outbox-for-order-events.md`, …)
- `docker-compose.yml`: local environment (added in M6)

## Module log
| Module | What it added to the capstone |
|---|---|
| M6 | — |
