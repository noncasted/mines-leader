using Shared;

namespace Framework.Tests.ClientTelemetry;

// The messages as a client writes them.
public static class TelemetryWire
{
    public static SharedClientTelemetry.Batch Batch(
        Guid session,
        string[]? names = null,
        (long Time, double?[] Values, bool Hidden)[]? samples = null,
        SharedClientTelemetry.Error[]? errors = null,
        (long Time, string Name)[]? marks = null)
    {
        return new SharedClientTelemetry.Batch
        {
            SessionId = session,
            Names = names?.Select(name => new SharedClientTelemetry.MetricName { Name = name, Unit = name.StartsWith("memory.") ? "bytes" : "ms" }).ToArray(),
            Samples = samples?.Select(sample => new SharedClientTelemetry.Sample { Time = sample.Time, Values = sample.Values.Select(value => value ?? double.NaN).ToArray(), Hidden = sample.Hidden }).ToArray(),
            Errors = errors,
            Marks = marks?.Select(mark => new SharedClientTelemetry.Mark { Time = mark.Time, Name = mark.Name }).ToArray()
        };
    }

    public static SharedClientTelemetry.Error Error(
        long time,
        SharedClientTelemetry.ErrorLevel level,
        string message,
        string? stack = null,
        int repeat = 1,
        string? category = null)
    {
        return new SharedClientTelemetry.Error { Time = time, Level = level, Message = message, Stack = stack!, Repeat = repeat, Category = category! };
    }
}
