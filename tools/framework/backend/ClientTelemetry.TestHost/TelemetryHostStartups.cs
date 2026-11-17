using System.Text.Json;
using Shared;

namespace Framework.ClientTelemetry.TestHost;

// The startup traces of the seed: a small synthetic one, or a file of ProfilerTraceData from the editor as a client would send it.
public static class TelemetryHostStartups
{
    private static readonly SharedClientTelemetry.Device _device = new() { Platform = "web", BuildVersion = "1.0.0", Cores = 8, ScreenWidth = 1920, ScreenHeight = 1080 };

    public static SharedClientTelemetry.StartupTrace Synthetic(bool final)
    {
        return new SharedClientTelemetry.StartupTrace
        {
            Device = _device,
            Name = "Boot",
            Final = final,
            DurationMs = 1500,
            Frames = 60,
            Spans =
            [
                new() { ParentIndex = -1, Name = "Boot", StartMs = 5, DurationMs = 1450, Frames = 60 },
                new() { ParentIndex = 0, Name = "Scope: Root", StartMs = 10, DurationMs = 600, Frames = 12 },
                new() { ParentIndex = 1, Name = "Assets: Common", StartMs = 20, DurationMs = 350, Frames = 8 },
                new() { ParentIndex = 1, Name = "Instantiate: Window", StartMs = 380, DurationMs = 120, Frames = 1 },
                new() { ParentIndex = 0, Name = "GET https://api.example.com/profile?token=abc", StartMs = 30, DurationMs = 400, Frames = 20 },
                new() { ParentIndex = 0, Name = "Scope: Next", StartMs = 700, DurationMs = 500, Frames = 20, Unfinished = !final }
            ]
        };
    }

    public static async Task<SharedClientTelemetry.StartupTrace> FromFile(string path)
    {
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = file.RootElement;
        var spans = root.GetProperty("Spans").EnumerateArray().ToArray();
        var indices = spans.Select((span, index) => (span.GetProperty("Id").GetInt32(), index)).ToDictionary(pair => pair.Item1, pair => pair.index);

        return new SharedClientTelemetry.StartupTrace
        {
            Device = _device,
            Name = root.GetProperty("Name").GetString()!,
            Final = true,
            DurationMs = root.GetProperty("DurationMs").GetDouble(),
            Frames = root.GetProperty("Frames").GetInt32(),
            Spans = spans.Select(span => new SharedClientTelemetry.StartupSpan
            {
                ParentIndex = span.GetProperty("ParentId").GetInt32() < 0 ? -1 : indices[span.GetProperty("ParentId").GetInt32()],
                Name = span.GetProperty("Name").GetString()!,
                StartMs = span.GetProperty("StartMs").GetDouble(),
                DurationMs = span.GetProperty("DurationMs").GetDouble(),
                Frames = span.GetProperty("Frames").GetInt32(),
                Unfinished = span.GetProperty("Unfinished").GetBoolean(),
                AllocBytes = span.GetProperty("AllocBytes").GetInt64()
            }).ToArray()
        };
    }
}
