namespace Infrastructure;

[GenerateSerializer]
public class MessageQueueSideEffect : ISideEffect 
{
    [Id(0)]
    public required string QueueName { get; set; }
    
    [Id(1)]
    public required object Message { get; set; }
    
    public Task Execute(IOrleans orleans)
    {
        var messageQueue = orleans.GetGrain<IMessageQueue>(QueueName);
        return messageQueue.Push(Message);
    }
}