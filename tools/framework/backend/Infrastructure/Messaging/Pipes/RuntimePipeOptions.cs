using Common;
using Shared;

namespace Infrastructure;

[GrainState(Table = "configs", State = "runtime_pipe_config", Lookup = "RuntimePipeConfig", Key = GrainKeyType.String)]
public class RuntimePipeOptions
{
    public int ObserverKeepAliveMinutes { get; init; } = 3;
    public int SendTimeoutSeconds { get; init; } = 10;
    public int SendRetryCount { get; init; } = 3;
    public int SendRetryBaseDelayMs { get; init; } = 500;
}
