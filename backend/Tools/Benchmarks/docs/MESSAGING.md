# Messaging Benchmarks

## Overview

Tests messaging infrastructure throughput: durable queues (direct and transactional), request-response pipes, and broadcast channels. Organized into three subgroups matching the messaging primitives.

## Metric

`msg/s` / `req/s` / `update/s` — calculated as `totalOperations / duration.TotalSeconds`.

---

## RuntimeChannel

### Broadcast throughput
- **File**: `Messaging/RuntimeChannelStressTest.cs`
- **Payload**: `MessageCount=75000`
- **What it measures**: RuntimeChannel broadcast throughput. 5 nodes publish via `PublishChannel`, root listens via `ListenChannel`. Total = MessageCount * 5.
- **Distributed**: Yes

### Distributed send throughput
- **File**: `Messaging/RuntimeChannelSendStressTest.cs`
- **Payload**: `MessageCount=75000`
- **What it measures**: RuntimeChannel broadcast throughput with per-message indexing and verbose logging. Same pattern as broadcast, 5 distributed nodes.
- **Distributed**: Yes

### Catch-up stress (disconnect/reconnect)
- **File**: `Messaging/RuntimeChannelCatchUpStressTest.cs`
- **Payload**: `MessageCount=5000`, `DisconnectCount=10`
- **What it measures**: Catch-up mechanism throughput under periodic disconnect/reconnect. Listener disconnects every N messages, reconnects, and catch-up replays missed messages from ring buffer.
- **Distributed**: No

### Delivery timeout (slow observers)
- **File**: `Messaging/RuntimeChannelDeliveryTimeoutTest.cs`
- **Payload**: `MessageCount=10000`, `SlowListenerCount=3`
- **What it measures**: Channel throughput when slow observers trigger delivery timeout. Verifies that slow observers are removed and fast observers continue receiving. Measures effective msg/s for fast path.
- **Distributed**: No

---

## RuntimePipe

### Request-response throughput
- **File**: `Messaging/RuntimePipeSendResponseStressTest.cs`
- **Payload**: `MessageCount=27000`
- **What it measures**: Request-response pipe throughput. Root sets up `AddPipeRequestHandler`, 5 nodes send `SendPipe` requests and await responses. Tests full round-trip latency. Total = MessageCount * 5.
- **Distributed**: Yes

### Retry stress (intermittent failures)
- **File**: `Messaging/RuntimePipeRetryStressTest.cs`
- **Payload**: `RequestCount=5000`, `FailureRate=0.3`, `Concurrency=50`
- **What it measures**: Pipe throughput under 30% handler failure rate with exponential backoff retry. Sends requests concurrently (50 in-flight). Measures effective req/s including retry overhead.
- **Distributed**: No

---

## DurableQueue

### Direct push throughput
- **File**: `Messaging/DurableQueueDirectStressTest.cs`
- **Payload**: `MessageCount=2000`
- **What it measures**: Durable queue throughput. 5 nodes each send MessageCount messages via `PushDirectQueue`, root listens and counts. Total = MessageCount * 5.
- **Distributed**: Yes

### Transactional push throughput
- **File**: `Messaging/DurableQueueTransactionalStressTest.cs`
- **Payload**: `MessageCount=2000`
- **What it measures**: Transactional queue throughput. Each node wraps `PushTransactionalQueue` in `orleans.InTransaction()`. Tests transaction + messaging overhead combined.
- **Distributed**: Yes

### Delivery throughput
- **File**: `Messaging/DurableQueueDeliveryTest.cs`
- **Payload**: `MessageCount=10000`
- **What it measures**: Durable queue delivery throughput. Root sends messages via `PushDirectQueue` and waits for all to arrive via listener.
- **Distributed**: No

### StateCollection update throughput
- **File**: `Messaging/StateCollectionUpdateStressTest.cs`
- **Payload**: `UpdateCount=5000`
- **What it measures**: StateCollectionUpdate delivery throughput via DurableQueue direct push. Measures update/s end-to-end from PushDirectQueue to listener callback.
- **Distributed**: No

---

## TODO

### Throughput (msg/s)
- [ ] **channel-fanout** — RuntimeChannel broadcast with 5/10/20 subscribers. Measures fan-out cost scaling. Distributed.
- [ ] **pipe-burst** — `SendPipe` burst: 1000 messages with no delay. Measures peak pipe throughput under saturation. Distributed.
- [ ] **queue-backpressure** — Durable queue with slow consumer (50ms processing per msg). Measures queue depth growth and delivery lag under backpressure.

### Correctness (ms)
- [ ] **queue-ordering** — Durable queue ordering guarantee: send 100 numbered messages, verify arrival order matches send order.
- [ ] **channel-reconnect** — RuntimeChannel subscription re-establishment after node restart. Measures re-subscription latency.
- [ ] **transactional-rollback** — Transactional queue message visibility: send inside transaction that rolls back, verify message NOT delivered.
