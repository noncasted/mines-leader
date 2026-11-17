using Game.ClientTelemetry;

namespace Console.Infrastructure.ClientTelemetry;

// A stored row of a kind of errors: a group keeps no stack. The memory of a session may have dropped its rows, so none may be left.
public sealed record TelemetryErrorExample(Guid Session, TelemetryStoredError Error)
{
    public static async Task<TelemetryErrorExample?> Find(IClientTelemetryReader reader, TelemetryGlobalErrorGroup group)
    {
        foreach (var session in group.SessionIds)
        {
            if (await FindInSession(reader, session, group.Fingerprint) is { } error)
                return new TelemetryErrorExample(session, error);
        }

        return null;
    }

    private static async Task<TelemetryStoredError?> FindInSession(IClientTelemetryReader reader, Guid session, string fingerprint)
    {
        var lastSeq = 0L;

        while (true)
        {
            var page = await reader.GetErrors(session, lastSeq, TelemetrySessionLog.PageRows);

            if (page.Count == 0)
                return null;

            if (page.FirstOrDefault(error => error.Fingerprint == fingerprint) is { } found)
                return found;

            lastSeq = page[^1].Seq;
        }
    }
}
