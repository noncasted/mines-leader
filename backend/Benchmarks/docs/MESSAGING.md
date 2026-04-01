# Messaging Benchmarks

## Overview

Tests messaging infrastructure throughput: durable queues (direct and transactional), request-response pipes, and broadcast channels. All distributed benchmarks spawn 5 nodes across services (Game, Meta, Coordinator, Silo, Console).

## Metric

`msg/s` — manually calculated as `totalMessages / stopwatch.Elapsed.TotalSeconds` in each benchmark's `Run()` method.

---

## Benchmarks

### Messaging direct queue
- **File**: `backend/Benchmarks/Messaging/MessagingDirectQueueStressTest.cs`
- **Payload**: `MessageCount=100`, `Delay=0.01f`
- **What it measures**: Durable queue throughput. 5 nodes each send MessageCount messages via `PushDirectQueue`, root listens and counts. Total = MessageCount * 5.
- **Distributed**: Yes

### Messaging transactional queue
- **File**: `backend/Benchmarks/Messaging/MessagingTransactionalQueueStressTest.cs`
- **Payload**: `MessageCount=100`, `Delay=0.01f`
- **What it measures**: Transactional queue throughput. Each node wraps `PushTransactionalQueue` in `orleans.InTransaction()`. Tests transaction + messaging overhead combined.
- **Distributed**: Yes

### Messaging pipe send with response (request-response)
- **File**: `backend/Benchmarks/Messaging/MessagePipeSendResponseStressTest.cs`
- **Payload**: `MessageCount=100`, `Delay=0.001f`
- **What it measures**: Request-response pipe throughput. Root sets up `AddPipeRequestHandler`, 5 nodes send `SendPipe` requests and await responses. Tests full round-trip latency.
- **Distributed**: Yes

### RuntimeChannel send (one-way broadcast)
- **File**: `backend/Benchmarks/Messaging/MessagePipeSendStressTest.cs`
- **Payload**: `MessageCount=100`, `Delay=0.001f`
- **What it measures**: RuntimeChannel broadcast throughput with detailed message indexing. 5 nodes publish via `PublishChannel`, root listens via `ListenChannel`.
- **Distributed**: Yes

### RuntimeChannel direct (one-way broadcast)
- **File**: `backend/Benchmarks/Messaging/RuntimeChannelStressTest.cs`
- **Payload**: `MessageCount=100`, `Delay=0.01f`
- **What it measures**: RuntimeChannel broadcast throughput. Same pattern as above but with higher delay between messages.
- **Distributed**: Yes

### durable-queue-delivery
- **File**: `backend/Benchmarks/Messaging/DurableQueueDeliveryTest.cs`
- **Payload**: EmptyPayload
- **What it measures**: Durable queue delivery correctness and speed. Sends 10 messages from root, waits for all to arrive via listener with 30s timeout.
- **Distributed**: No

---

## TODO

### Throughput (msg/s)
- [ ] **messaging-channel-fanout** — `RuntimeChannel` broadcast with 5/10/20 subscribers. Measures fan-out cost scaling. Distributed.
- [ ] **messaging-pipe-burst** — `SendPipe` burst: 1000 messages with no delay. Measures peak pipe throughput under saturation. Distributed.
- [ ] **messaging-queue-backpressure** — Durable queue with slow consumer (50ms processing per msg). Measures queue depth growth and delivery lag under backpressure.

### Correctness (ms)
- [ ] **messaging-queue-ordering** — Durable queue ordering guarantee: send 100 numbered messages, verify arrival order matches send order.
- [ ] **messaging-channel-reconnect** — RuntimeChannel subscription re-establishment after node restart. Measures re-subscription latency.
- [ ] **messaging-transactional-rollback** — Transactional queue message visibility: send inside transaction that rolls back, verify message NOT delivered.
