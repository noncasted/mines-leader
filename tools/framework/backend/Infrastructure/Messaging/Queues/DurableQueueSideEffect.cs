namespace Infrastructure;

[GenerateSerializer]
public class DurableQueueSideEffect : ISideEffect, ICorrelatedSideEffect
{
    [Id(0)]
    public required string QueueName { get; init; }

    [Id(1)]
    public required object Message { get; init; }

    [Id(2)]
    public Guid CorrelationId { get; init; }

    // Stored with the side effect, the same on every retry: the queue delivers a retry only to the observers that
    // have not got the message yet.
    [Id(3)]
    public Guid MessageId { get; init; } = Guid.NewGuid();

    public Task Execute(IOrleans orleans)
    {
        var queue = orleans.GetGrain<IDurableQueue>(QueueName);
        return queue.Push(Message, CorrelationId, MessageId);
    }
}
