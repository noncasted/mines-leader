using Common;
using Shared;

namespace Infrastructure;

[GrainState(Table = "configs", State = "message_queue_config", Lookup = "DurableQueueConfig",
    Key = GrainKeyType.String)]
// A record: the console edits a config with `with` on its own copy.
public record DurableQueueOptions
{
    public int ObserverKeepAliveMinutes { get; init; } = 3;
}
