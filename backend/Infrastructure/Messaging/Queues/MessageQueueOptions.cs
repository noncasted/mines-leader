namespace Infrastructure;

public class DurableQueueOptions
{
    public int ObserverKeepAliveMinutes { get; set; } = 3;
}

public interface IDurableQueueConfig : IAddressableState<DurableQueueOptions> { }
