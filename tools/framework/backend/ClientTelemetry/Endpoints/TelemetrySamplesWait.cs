namespace Game.ClientTelemetry;

// The long poll asks the grain of the session, from its memory, whichever gateway the client and the reader came to.
internal static class TelemetrySamplesWait
{
    private static readonly TimeSpan _step = TimeSpan.FromMilliseconds(500);

    // Empty: nothing came in the time, or the reader went away.
    public static async Task<IReadOnlyList<TelemetryStoredSample>> Next(
        IClientTelemetryReader reader,
        Guid session,
        long after,
        int limit,
        TimeSpan wait,
        CancellationToken cancellation)
    {
        var deadline = DateTime.UtcNow + wait;

        while (true)
        {
            var samples = await reader.GetSamples(session, after, limit);
            var left = deadline - DateTime.UtcNow;

            if (samples.Count > 0 || left <= TimeSpan.Zero)
                return samples;

            // Nothing more will come, and a closed session is read from the database.
            if (await reader.GetSession(session) is not { Status: TelemetrySessionStatus.Live })
                return samples;

            try
            {
                await Task.Delay(left < _step ? left : _step, cancellation);
            }
            catch (OperationCanceledException)
            {
                return samples;
            }
        }
    }
}
