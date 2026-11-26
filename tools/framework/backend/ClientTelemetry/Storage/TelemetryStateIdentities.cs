using Infrastructure.State;

namespace Game.ClientTelemetry;

internal static class TelemetryStateIdentities
{
    public static StateIdentity IdentityOf<T>(this IStateStorage storage, Guid id) where T : IStateValue
    {
        var info = storage.Registry.Get<T>();

        return new StateIdentity
        {
            Key = id,
            Type = info.Name,
            TableName = info.TableName,
            Extension = null
        };
    }
}
