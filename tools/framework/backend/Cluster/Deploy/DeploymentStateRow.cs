using Infrastructure;
using Infrastructure.State;

namespace Cluster.Deploy;

// The row of a deployment state: an AddressableStateValue whose UpdateDate grows strictly. Readers drop a value not
// newer than the last one applied, so a stamp is later than the previous one even when the clock of this host is
// behind the clock of the host that wrote it.
public static class DeploymentStateRow
{
    public static AddressableStateValue Create<T>(IStateSerializer serializer, T value, DateTime previous)
    {
        var now = DateTime.UtcNow;

        return new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = now > previous ? now : previous.AddTicks(1),
            Value = serializer.Serialize(value)
        };
    }

    // Read-modify-write of the row of a deploy, then the push on its channel. Called by a non-reentrant writer grain
    // keyed by the deploy id: the grain serializes the writes of every service.
    public static async Task<AddressableStateValue> Change<T>(
        IOrleans orleans,
        IMessaging messaging,
        Guid deployId,
        StateIdentity identity,
        Func<T, Task> change) where T : class, new()
    {
        // Read on every call: the activation may have moved to another silo since the last write.
        var stored = await orleans.StateStorage.Read<AddressableStateValue>(identity);

        var value = stored.IsInitialized
            ? orleans.Serializer.Deserialize<T>(stored.Value)
            : new T();

        await change(value);

        var next = Create(orleans.Serializer, value, stored.UpdateDate);

        await orleans.StateStorage.Write(identity, next);
        await messaging.PublishChannel(new DeploymentStateChannelId<T>(deployId), next);

        return next;
    }
}
