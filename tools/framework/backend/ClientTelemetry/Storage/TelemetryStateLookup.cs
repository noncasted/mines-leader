using Infrastructure;
using Infrastructure.State;

namespace Game.ClientTelemetry;

internal static class TelemetryStateLookup
{
    // Null: no such key. The collection answers first; a key it has not heard about yet is looked for in its row, which is written
    // before the client is answered. So the database is asked only for garbage and in this short moment.
    public static async Task<T?> Find<T>(this IStateCollection<Guid, T> known, IStateStorage storage, Guid id, Func<T, bool> isStored)
        where T : class, IStateValue, new()
    {
        if (known.TryGetValue(id, out var value))
            return value;

        var stored = await storage.Read<T>(storage.IdentityOf<T>(id));
        return isStored(stored) ? stored : null;
    }
}
