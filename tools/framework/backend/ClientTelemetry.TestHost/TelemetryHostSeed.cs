using Framework.Tests.ClientTelemetry;
using Shared;

namespace Framework.ClientTelemetry.TestHost;

// Sessions for a look at the reading without a Unity client, through the real socket: closed ones of several platforms and tags with
// errors and marks, and one left open on a socket that stays, which the host closes as lost.
public static class TelemetryHostSeed
{
    private const int _closedSessions = 6;
    private const int _samplesPerSession = 30;

    // Kept to the end of the process, as a client whose gateway stopped hearing from it: the timer of the grain closes its session.
    private static TelemetryMetaClient? _openClient;

    public static async Task Run(Uri host, string? startupFile)
    {
        await using (var client = await TelemetryMetaClient.Connect(host))
        {
            var trace = startupFile == null ? TelemetryHostStartups.Synthetic(final: true) : await TelemetryHostStartups.FromFile(startupFile);
            var startup = await client.Request<SharedClientTelemetry.StartupTraceResult>(trace);

            for (var i = 0; i < _closedSessions; i++)
            {
                var session = await SendSession(client, i, i % 2 == 0 ? "web" : "steam", i % 2 == 0 ? ["run", "seed"] : ["menu"], startup.StartupId);
                await client.Request<EmptyResponse>(new SharedClientTelemetry.SessionEnd { SessionId = session });
            }
        }

        _openClient = await TelemetryMetaClient.Connect(host);

        // A snapshot that stays one: the trace of this client never ends.
        var snapshot = await _openClient.Request<SharedClientTelemetry.StartupTraceResult>(TelemetryHostStartups.Synthetic(final: false));
        await SendSession(_openClient, _closedSessions, "web", ["run"], snapshot.StartupId);
    }

    private static async Task<Guid> SendSession(TelemetryMetaClient client, int index, string platform, string[] tags, Guid startupId)
    {
        var started = await client.Request<SharedClientTelemetry.SessionStarted>(new SharedClientTelemetry.SessionStart
        {
            Device = new SharedClientTelemetry.Device { Platform = platform, BuildVersion = "1.0.0", Cores = 4 + index, ScreenWidth = 1920, ScreenHeight = 1080 },
            Tags = tags,
            StartupId = startupId
        });

        await client.Request<SharedClientTelemetry.BatchResult>(new SharedClientTelemetry.Batch
        {
            SessionId = started.SessionId,
            Names =
            [
                new SharedClientTelemetry.MetricName { Name = "frame.fps", Unit = "count" },
                new SharedClientTelemetry.MetricName { Name = "memory.mono.used", Unit = "bytes" }
            ],
            Samples = Samples(index)
        });

        await client.Request<SharedClientTelemetry.BatchResult>(new SharedClientTelemetry.Batch
        {
            SessionId = started.SessionId,
            Marks = [new SharedClientTelemetry.Mark { Time = 1000, Name = "level.start" }, new SharedClientTelemetry.Mark { Time = 20000, Name = "level.end" }],
            Errors = Errors(index)
        });

        return started.SessionId;
    }

    private static SharedClientTelemetry.Sample[] Samples(int index)
    {
        return Enumerable.Range(0, _samplesPerSession)
                         .Select(second => new SharedClientTelemetry.Sample
                         {
                             Time = second * 1000L,
                             Values = [60 - index * 8 - second % 5, 100_000_000 + second * 500_000L],
                             Hidden = second == _samplesPerSession - 1
                         })
                         .ToArray();
    }

    private static SharedClientTelemetry.Error[] Errors(int index)
    {
        var common = TelemetryWire.Error(5000, SharedClientTelemetry.ErrorLevel.Error, "Seed failure of the common kind", "at Seed.Common ()", 1 + index);

        return index % 3 == 0
            ? [common, TelemetryWire.Error(7000, SharedClientTelemetry.ErrorLevel.Exception, "Seed failure of a rare kind", "at Seed.Rare ()")]
            : [common];
    }
}
