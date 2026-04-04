namespace Infrastructure;

public class RuntimePipeOptions
{
    public int ObserverKeepAliveMinutes { get; set; } = 3;
    public int SendTimeoutSeconds { get; set; } = 30;
}

public interface IRuntimePipeConfig : IAddressableState<RuntimePipeOptions>
{
}