namespace Infrastructure;

[GenerateSerializer]
public class DurableQueueSideEffect : ISideEffect
{
    [Id(0)]
    public required string QueueName { get; set; }

    [Id(1)]
    public required object Message { get; set; }

    public Task Execute(IOrleans orleans)
    {
        var queue = orleans.GetGrain<IDurableQueue>(QueueName);
        return queue.Push(Message);
    }
}