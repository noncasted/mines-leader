# Infrastructure: Messaging

Inter-service messaging system. Built on Orleans Grains using the Observer pattern.

## Two Channel Types

### Queue (1:N) - Message Queue

One sender, many receivers. Messages are delivered to all subscribers.

```
Publisher ------> MessageQueue Grain ------> Observer A
                        |                    Observer B
                        |                    Observer C
                        +------------------------>
```

**Use cases:**
- Service Discovery heartbeats
- ClusterState updates
- Coordinator events

### Pipe (1:1) - Point-to-Point

One sender, one receiver. Supports request-response.

```
Sender ------> MessagePipe Grain ------> Single Observer
       <------------------------------+ (response)
```

**Use cases:**
- Request-response between services
- Direct communication with specific service

## API

### IMessaging (Main Interface)

```csharp
public interface IMessaging {
    IMessageQueueClient Queue { get; }
    IMessagePipeClient Pipe { get; }
    Task Start(IReadOnlyLifetime lifetime);
}

// Extension methods on IMessaging:
messaging.PushTransactionalQueue(id, message);  // Within Orleans transaction
messaging.PushDirectQueue(id, message);         // Immediate send
messaging.ListenQueue<T>(lifetime, id, handler);

messaging.SendPipe(id, message);                // One-way
messaging.SendPipe<TResponse>(id, message);     // Request-response
messaging.ListenPipe<T>(lifetime, id, handler);
messaging.AddPipeRequestHandler<TReq, TRes>(lifetime, id, handler);
```

### Channel IDs

```csharp
// Simple ID
var queueId = new MessageQueueId("my-queue");
var pipeId = new MessagePipeId("my-pipe");

// Service-specific pipe
var servicePipeId = new MessagePipeServiceRequestId(serviceOverview, typeof(MyRequest));
// Result: "service-pipe-request-{serviceId}-{typeName}"
```

## Architecture

### Queue (MessageQueue grain)

```csharp
// Grain stores list of observers
private readonly Dictionary<Guid, ObserverData> _observers = new();

// On message receive - broadcast to all
protected override async Task Process(IReadOnlyList<object> entries) {
    await Task.WhenAll(_observers.Values.Select(o => o.Observer.Send(entries)));
}
```

**Features:**
- Inherits from `BatchWriter` - messages are accumulated and sent in batches
- Observers are automatically removed on send error
- Grain does not deactivate if observer was recently set (< 3 min)

### Pipe (MessagePipe grain)

```csharp
// Grain stores single observer
private IMessagePipeObserver? _observer;

// One-way
public async Task Send(object message) {
    await _observer!.Send(message);
}

// Request-response
public async Task<TResponse> Send<TResponse>(object message) {
    return await _observer!.Send<TResponse>(message);
}
```

**Features:**
- Only one observer per pipe
- Supports request-response
- Grain does not deactivate if observer was recently set (< 3 min)

### Client-side Resubscription

Clients automatically resubscribe every 10 seconds:

```csharp
// MessageQueueClient / MessagePipeClient
private async Task ResubscribeLoop(IReadOnlyLifetime lifetime) {
    while (lifetime.IsTerminated == false) {
        await Task.WhenAll(_resubscribeActions.Select(t => t()));
        await Task.Delay(TimeSpan.FromSeconds(10), lifetime.Token);
    }
}
```

This protects against subscription loss on grain restart.

## Rules

1. **Transactional vs Direct**
   - `PushTransactionalQueue` - message is sent only on successful Orleans transaction commit
   - `PushDirectQueue` - immediate send

2. **Lifetime management** - always pass lifetime to ListenQueue/ListenPipe

3. **Resubscription interval = 10 seconds**

4. **Grain keep-alive = 3 minutes** after observer is set

## Registration

```csharp
builder.AddMessaging();  // Registers IMessaging, Queue and Pipe clients
```

## Logging

| Tag | Component | Description |
|-----|-----------|-------------|
| `[Messaging] [Queue]` | MessageQueue, MessageQueueClient | Queues |
| `[Messaging] [Pipe]` | MessagePipe, MessagePipeClient | Pipes |

## Key Files

| File | Purpose |
|------|---------|
| `Messaging/Interfaces/IMessaging.cs` | Main interface + IDs |
| `Messaging/Service/Messaging.cs` | IMessaging implementation |
| `Messaging/Queues/Grains/MessageQueue.cs` | Queue grain |
| `Messaging/Queues/Service/MessageQueueClient.cs` | Queue client |
| `Messaging/Pipes/Grains/MessagePipe.cs` | Pipe grain |
| `Messaging/Pipes/Service/MessagePipeClient.cs` | Pipe client |

## Typical Scenarios

### Broadcast to All Services

```csharp
// Send
await messaging.PushDirectQueue(
    new MessageQueueId("my-broadcast"),
    new MyEvent { Data = "hello" }
);

// Subscribe
messaging.ListenQueue<MyEvent>(lifetime, new MessageQueueId("my-broadcast"), evt => {
    Console.WriteLine(evt.Data);
});
```

### Request-Response to Specific Service

```csharp
// Server (register handler)
var pipeId = new MessagePipeServiceRequestId(discovery.Self, typeof(MyRequest));
await messaging.AddPipeRequestHandler<MyRequest, MyResponse>(lifetime, pipeId, async req => {
    return new MyResponse { Result = "processed" };
});

// Client (send request)
var response = await messaging.SendPipe<MyResponse>(pipeId, new MyRequest());
```
