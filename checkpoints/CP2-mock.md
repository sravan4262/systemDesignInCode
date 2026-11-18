# CP2 mock: Phase 2 (M6–M15), 30 shuffled questions

Started 2026-11-17 on Mac. Graded /10 each. Batches of 5.

## Batch 1 (23/40 ≈ 5.8, Q2 skipped)
| # | Question | Score | Gap / correction |
|---|---|---|---|
| 1 | POST /payments timed out: retry? | 8 | Timeout = unknown outcome → DO retry with the SAME key; provider replays the original result. Add backoff + cap. |
| 2 | Lost update under RCSI | skipped | Learner: RCSI rarely asked. Core answer: atomic conditional UPDATE (`WHERE Qty >= 1`), UPDLOCK, or rowversion. |
| 3 | Kafka 6 partitions, 10 instances | 2 | ❌ again said round-robin. Partitions are assigned to instances: 6 busy, 4 idle. Round-robin = RabbitMQ. |
| 4 | Popular key expires, 5,000 hit the DB | 5 | Name = cache stampede / thundering herd. Mechanism: single-flight lock (others wait/serve stale), early refresh, TTL jitter. |
| 5 | Dual write + outbox | 8 | Add both failure directions (lost vs. ghost) and at-least-once → inbox. |

## Batch 2 (31/50 = 6.2)
| # | Question | Score | Gap / correction |
|---|---|---|---|
| 6 | /orders/1002 shows Bob's order | 6 | Name = BOLA (OWASP API #1). Systematic: ownership in the query, user id only from token, EF global filters / GetForUser, 404, per-endpoint test. |
| 7 | Stale profile after update (replicas) | 6 | Cause ✅. Returning data in the PUT doesn't survive a refresh (new GET → replica). Fix = read-your-writes routing (primary for that user for N s, or by LSN). |
| 8 | PKCE stolen code | 8 | Repeat slip: first request carries the HASH of the secret; the secret only at token exchange. |
| 9 | Orders/Inventory deadlock without locks | 3 | Retry is the safety net, not the fix (victim is rolled back). Fix = consistent lock order; short tx, indexes, RCSI; retry 1205. |
| 10 | Trace through a queue | 8 | Inject traceparent into MESSAGE HEADERS, consumer extracts + starts span with that parent; store traceparent in outbox row. |

## Batch 3 (18/50 = 3.6; 2026-11-18) · running total 72/140 ≈ 5.1
| # | Question | Score | Gap / correction |
|---|---|---|---|
| 11 | Crash after commit, before ack | 6 | Redelivered (DLQ only after repeated failures); INSERT Inbox → unique violation → skip → ACK. |
| 12 | Why not 2PC | 0 | Didn't know 2PC. Prepare (lock + vote) → commit; locks held, coordinator crash blocks participants, brokers/cloud DBs don't support it, couples availability → Saga + compensations + outbox/inbox. |
| 13 | Redis lock + fencing | 0 | Didn't know. Lease expires while holder paused → two holders; fencing = increasing token checked by the RESOURCE (`WHERE Fence < @token`). |
| 14 | Shard key | 5 | Tenant id ✅. Properties: high cardinality, even spread, in most queries, keeps related data together. Bad: low cardinality, monotonic (hot last shard), not in queries (fan-out). Hot tenant → dedicated shard via directory. |
| 15 | Cache-aside write: update or delete | 7 | Delete ✅ (concurrent writers race). Delete only, let the next read fill it; TTL caps the slow-reader race. |

## Batch 4 (23/50 = 4.6) · running total 95/190 = 5.0
| # | Question | Score | Gap / correction |
|---|---|---|---|
| 16 | Revoke a JWT early | 5 | Short-lived ✅ (more frequent refreshes, not more refresh tokens). Missing: revoke refresh token at Entra → current token valid until exp, next refresh fails; denylist (jti) / CAE for zero window. |
| 17 | 3 layers × 3 retries | 7 | Retry at the closest layer ✅. Add 4×4×4 = 64 and breaker/retry budget. |
| 18 | Index (A,B,C) seeks | 6 | A+B seek ✅; missed A+C also seeks on A (C filtered); B alone scans. Rule: seek on any left prefix. |
| 19 | Kafka poison message | 2 | Service isn't down: one message always fails → offset can't be committed → partition stuck. Fix: permanent → no retry → DLQ topic → commit offset → alert/replay. |
| 20 | Breaker states | 3 | ❌ THIRD time reversed. Failures → OPEN (no calls) → after break → HALF-OPEN (ONE trial) → CLOSED / OPEN. |

## Batch 5 (19/50 = 3.8) · running total 114/240 = 4.75
| # | Question | Score | Gap / correction |
|---|---|---|---|
| 21 | Cursor vs. offset | 0 | Didn't know (2nd time). Offset reads + discards, slows with depth, shifts on inserts; cursor = WHERE after last seen + index seek, stable; offset for small/jump-to-page, cursor for feeds/APIs. |
| 22 | RabbitMQ vs. Kafka | 8 | Kafka replay/audit ✅, RabbitMQ commands ✅. Add: Kafka many/future consumers + per-key order; RabbitMQ per-message retry/DLQ/delay/routing. |
| 23 | Flash sale claim | 7 | Atomic UPDATE (correct) + buckets (contention) ✅. Buckets don't keep losers off the DB → Redis DECR pre-check / waiting room. |
| 24 | Redis down | 4 | Falling back to the DB is the DANGER (DB absorbs 90%+ → falls over). Short timeout + breaker, bulkhead/coalescing/in-memory cache for DB fallbacks, shed/degrade. |
| 25 | Inbox unique constraint vs. IF NOT EXISTS | 0 | Not answered (repeat topic). Check-then-act race; unique constraint in the same tx = DB referee → duplicate-key → skip + ack. |

## Batch 6 (22/50 = 4.4)
| # | Question | Score | Gap / correction |
|---|---|---|---|
| 26 | Sync vs. async replication | 5 | Sync waits for ≥1 sync replica (not all). Trade-off: sync = no loss, slower writes; async = fast, can lose last seconds on failover. Failover: writes fail briefly → retry + idempotency; unshipped async logs are lost, not replayed. |
| 27 | 3 outbox relays | 5 | Duplicates ✅; SentAt alone doesn't stop concurrent reads → UPDLOCK+READPAST / single active relay / partition by key; ordering breaks; still at-least-once → inbox. |
| 28 | Consumer falling behind metric | 0 | Not answered. Lag / growing depth; oldest message age = staleness users feel (depth = volume). |
| 29 | Elasticsearch sync | 6 | Event-driven ✅. Via outbox or CDC (not a dual write), idempotent versioned upserts, reconciliation/reindex, eventual consistency. |
| 30 | 3M-row nightly update | 6 | Table lock (escalation) ✅, batches ✅; parallel ❌ again → sequential small batches with pauses, off-peak. |

## Result: 136/290 ≈ 4.7/10 (29 graded, Q2 skipped)
- **Strong (7+):** Q1 payment retry, Q5 outbox, Q8 PKCE, Q10 trace through queue, Q15 cache delete, Q17 retry amplification, Q22 RabbitMQ vs. Kafka, Q23 flash sale.
- **Weak (≤3), retry before Phase 3:** Q3 Kafka partitions vs. instances (2nd time), Q20 breaker order (3rd time), Q9 deadlock = lock order, Q19 poison message, Q21 cursor vs. offset (2nd time), Q25 inbox unique constraint, Q12 2PC, Q13 Redis fencing, Q28 lag/oldest age.
- **Pattern:** concepts understood; points lost on specific mechanisms already taught but not locked in (state order, partition assignment, lock order, check-then-act).
