namespace Infrastructure;

public class RuntimeChannelOptions
{
    public int ObserverKeepAliveMinutes { get; set; } = 3;
}

public interface IRuntimeChannelConfig : IAddressableState<RuntimeChannelOptions> { }
