namespace Infrastructure;

public class MessageQueueOptions
{
    public int ObserverKeepAliveMinutes { get; set; } = 3;
}

public interface IMessageQueueConfig : IAddressableState<MessageQueueOptions> { }
