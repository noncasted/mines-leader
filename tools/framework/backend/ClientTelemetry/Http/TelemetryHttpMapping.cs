using Shared;

namespace Game.ClientTelemetry;

// The HTTP JSON as the messages of the socket: from here on both ways are the same (TelemetryBatchParser checks everything). The
// client writes no null (JsonUtility): an empty text is none, HasPrevious and HasDuration say what a null would; a null item of a
// list stays null and is rejected by the parser like one of the socket.
internal static class TelemetryHttpMapping
{
    public static SharedClientTelemetry.SessionStart ToSessionStart(HttpTelemetrySessionStart start)
    {
        return new SharedClientTelemetry.SessionStart
        {
            Device = ToDevice(start.Device),
            Tags = start.Tags,
            StartupId = start.StartupId,
            Previous = start.HasPrevious && start.Previous != null ? ToPrevious(start.Previous) : null
        };
    }

    public static SharedClientTelemetry.Batch ToBatch(HttpTelemetryBatch batch)
    {
        return new SharedClientTelemetry.Batch
        {
            SessionId = batch.SessionId,
            Names = Map(batch.Names, ToName),
            Samples = Map(batch.Samples, ToSample),
            Errors = Map(batch.Errors, ToError),
            Marks = Map(batch.Marks, ToMark)
        };
    }

    public static SharedClientTelemetry.SessionEnd ToSessionEnd(HttpTelemetrySessionEnd end)
    {
        return new SharedClientTelemetry.SessionEnd { SessionId = end.SessionId };
    }

    public static SharedClientTelemetry.StartupTrace ToStartupTrace(HttpTelemetryStartupTrace trace)
    {
        return new SharedClientTelemetry.StartupTrace
        {
            StartupId = trace.StartupId,
            Device = ToDevice(trace.Device),
            Name = trace.Name,
            Final = trace.Final,
            DurationMs = trace.DurationMs,
            Frames = trace.Frames,
            Spans = Map(trace.Spans, ToStartupSpan)
        };
    }

    // The slots in Empty are NaN, no value, as on the socket.
    public static SharedClientTelemetry.Sample ToSample(HttpTelemetrySample sample)
    {
        var values = sample.Values?.ToArray();

        if (values != null && sample.Empty != null)
        {
            foreach (var slot in sample.Empty)
            {
                if (slot >= 0 && slot < values.Length)
                    values[slot] = double.NaN;
            }
        }

        return new SharedClientTelemetry.Sample
        {
            Time = sample.Time,
            Values = values,
            Hidden = sample.Hidden
        };
    }

    private static SharedClientTelemetry.PreviousSession ToPrevious(HttpTelemetryPreviousSession previous)
    {
        return new SharedClientTelemetry.PreviousSession
        {
            SessionId = previous.SessionId,
            Tags = previous.Tags,
            LastSampleTime = previous.LastSampleTime,
            SinceMs = previous.SinceMs,
            Marks = Map(previous.Marks, ToMark),
            SampleNames = previous.SampleNames,
            Samples = Map(previous.Samples, ToSample)
        };
    }

    private static SharedClientTelemetry.Device ToDevice(HttpTelemetryDevice? device)
    {
        if (device == null)
            return new SharedClientTelemetry.Device();

        return new SharedClientTelemetry.Device
        {
            Platform = Text(device.Platform),
            BuildVersion = Text(device.BuildVersion),
            Browser = Text(device.Browser),
            BrowserMajor = device.BrowserMajor,
            Os = Text(device.Os),
            OsMajor = device.OsMajor,
            Gpu = Text(device.Gpu),
            Cores = device.Cores,
            MemoryClass = device.MemoryClass,
            ScreenWidth = device.ScreenWidth,
            ScreenHeight = device.ScreenHeight,
            Dpr = device.Dpr
        };
    }

    private static SharedClientTelemetry.MetricName ToName(HttpTelemetryName name)
    {
        return new SharedClientTelemetry.MetricName { Name = name.Name, Unit = Text(name.Unit) };
    }

    private static SharedClientTelemetry.Error ToError(HttpTelemetryError error)
    {
        return new SharedClientTelemetry.Error
        {
            Time = error.Time,
            Level = error.Level switch
            {
                "Error" => SharedClientTelemetry.ErrorLevel.Error,
                "Exception" => SharedClientTelemetry.ErrorLevel.Exception,
                "Warning" => SharedClientTelemetry.ErrorLevel.Warning,
                _ => default
            },
            Message = error.Message,
            Stack = Text(error.Stack),
            Repeat = error.Repeat,
            Category = Text(error.Category)
        };
    }

    private static SharedClientTelemetry.Mark ToMark(HttpTelemetryMark mark)
    {
        return new SharedClientTelemetry.Mark
        {
            Time = mark.Time,
            Name = mark.Name,
            Detail = Text(mark.Detail),
            DurationMs = mark.HasDuration ? mark.DurationMs : null
        };
    }

    private static SharedClientTelemetry.StartupSpan ToStartupSpan(HttpTelemetryStartupSpan span)
    {
        return new SharedClientTelemetry.StartupSpan
        {
            ParentIndex = span.ParentIndex,
            Name = span.Name,
            StartMs = span.StartMs,
            DurationMs = span.DurationMs,
            Frames = span.Frames,
            Unfinished = span.Unfinished,
            AllocBytes = span.AllocBytes,
            File = Text(span.File),
            TransferBytes = span.TransferBytes,
            Cached = span.Cached
        };
    }

    private static string? Text(string? text)
    {
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static TOut[]? Map<TIn, TOut>(TIn?[]? items, Func<TIn, TOut> convert) where TIn : class where TOut : class
    {
        return items?.Select(item => item == null ? null! : convert(item)).ToArray();
    }
}
