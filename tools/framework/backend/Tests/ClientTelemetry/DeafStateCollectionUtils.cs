using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;

namespace Framework.Tests.ClientTelemetry;

// The collection of another host that misses the notifications while it is deaf: what a failed push leaves on the other hosts.
public sealed class DeafStateCollectionUtils<TKey, TValue> : StateCollectionUtils<TKey, TValue>
    where TKey : notnull
    where TValue : class, IStateValue, new()
{
    public DeafStateCollectionUtils(IStateStorage storage, IMessaging messaging)
        : base(storage.Registry, storage, messaging, NullLogger<StateCollectionUtils<TKey, TValue>>.Instance)
    {
    }

    private volatile bool _isDeaf;

    public void Deafen() => _isDeaf = true;
    public void Hear() => _isDeaf = false;

    public override Task ListenUpdates(Internal.IReadOnlyLifetime lifetime, Func<TKey, Task> onUpdate)
    {
        return base.ListenUpdates(lifetime, key => _isDeaf ? Task.CompletedTask : onUpdate(key));
    }
}
