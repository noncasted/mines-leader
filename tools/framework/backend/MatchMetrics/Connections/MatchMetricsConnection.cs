using Microsoft.Extensions.Logging;
using Shared;

namespace Game.MatchMetrics;

// The client streams of one socket: a batch reaches only a stream opened on this socket, so a client writes only as its own
// user. The calls run one at a time (the handlers of a socket run side by side). A batch that breaks the contract is
// Rejected; a failure of the database or of the admission throws, the request fails and the client sends the batch again.
public sealed class MatchMetricsConnection
{
    internal MatchMetricsConnection(
        Guid userId,
        IMatchMetricsAdmission admission,
        MatchMetricStreamStore streams,
        MatchMetricBatchWriter writer,
        MatchMetricsOptions options,
        TimeProvider time,
        ILogger<MatchMetricsConnection> logger)
    {
        _userId = userId;
        _admission = admission;
        _streams = streams;
        _writer = writer;
        _options = options;
        _time = time;
        _logger = logger;
    }

    private readonly Guid _userId;
    private readonly IMatchMetricsAdmission _admission;
    private readonly MatchMetricStreamStore _streams;
    private readonly MatchMetricBatchWriter _writer;
    private readonly MatchMetricsOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<MatchMetricsConnection> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, OpenStream> _open = new();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    public async Task<SharedMatchMetrics.Opened> Open(SharedMatchMetrics.Open request)
    {
        await _gate.WaitAsync();

        try
        {
            if (request.Kind != SharedMatchMetrics.MatchStreamKind.Client)
                return Opened(SharedMatchMetrics.MatchMetricsStatus.Rejected);

            var now = Now;
            var admit = await _admission.Check(request.MatchId, _userId);
            var status = StatusOf(admit, now);

            if (status != SharedMatchMetrics.MatchMetricsStatus.Ok)
                return Opened(status);

            var stream = await _streams.Open(request.MatchId, _userId, request.WriterId, request.Kind, now);
            _open[stream.Id] = new OpenStream(stream, admit, now);

            return new SharedMatchMetrics.Opened
            {
                Status = SharedMatchMetrics.MatchMetricsStatus.Ok,
                StreamId = stream.Id,
                LastSeq = stream.LastSeq
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SharedMatchMetrics.BatchResult> Accept(SharedMatchMetrics.Batch batch)
    {
        await _gate.WaitAsync();

        try
        {
            if (_open.TryGetValue(batch.StreamId, out var open) == false)
                return Result(SharedMatchMetrics.MatchMetricsStatus.Rejected);

            var now = Now;

            if (open.Admit.ClosedAt == null && now - open.CheckedAt > _options.AdmissionRecheck)
            {
                open = open with { Admit = await _admission.Check(open.Stream.MatchId, _userId), CheckedAt = now };
                _open[batch.StreamId] = open;
            }

            var status = StatusOf(open.Admit, now);

            if (status != SharedMatchMetrics.MatchMetricsStatus.Ok)
                return Result(status == SharedMatchMetrics.MatchMetricsStatus.NotInMatch ? SharedMatchMetrics.MatchMetricsStatus.Rejected : status);

            return Result(await Write(open.Stream, batch, now));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SharedMatchMetrics.MatchMetricsStatus> Write(MatchMetricStream stream, SharedMatchMetrics.Batch batch, DateTime now)
    {
        // Before the parse: a copy declares again the slots its first copy stored.
        if (batch.Seq <= stream.LastSeq)
            return SharedMatchMetrics.MatchMetricsStatus.Duplicate;

        try
        {
            var parsed = MatchMetricsBatchParser.Parse(batch, stream.Kinds);

            return await _writer.Write(stream, parsed, now) switch
            {
                MatchMetricWrite.Stored => SharedMatchMetrics.MatchMetricsStatus.Ok,
                MatchMetricWrite.Duplicate => SharedMatchMetrics.MatchMetricsStatus.Duplicate,
                MatchMetricWrite.Gap => Rejected(stream, batch.Seq, $"a gap after the batch {stream.LastSeq}"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        catch (MatchMetricsRejectedException exception)
        {
            return Rejected(stream, batch.Seq, exception.Message);
        }
    }

    private SharedMatchMetrics.MatchMetricsStatus Rejected(MatchMetricStream stream, int seq, string reason)
    {
        _logger.LogWarning("[MatchMetrics] The batch {Seq} of the stream {Stream} of the match {Match} is rejected: {Reason}",
            seq, stream.Id, stream.MatchId, reason);

        return SharedMatchMetrics.MatchMetricsStatus.Rejected;
    }

    private SharedMatchMetrics.MatchMetricsStatus StatusOf(MatchMetricsAdmit admit, DateTime now)
    {
        if (admit.IsPlayer == false)
            return SharedMatchMetrics.MatchMetricsStatus.NotInMatch;

        if (admit.ClosedAt is { } closedAt && now - closedAt > _options.CloseGrace)
            return SharedMatchMetrics.MatchMetricsStatus.Closed;

        return SharedMatchMetrics.MatchMetricsStatus.Ok;
    }

    private static SharedMatchMetrics.Opened Opened(SharedMatchMetrics.MatchMetricsStatus status)
    {
        return new SharedMatchMetrics.Opened { Status = status };
    }

    private static SharedMatchMetrics.BatchResult Result(SharedMatchMetrics.MatchMetricsStatus status)
    {
        return new SharedMatchMetrics.BatchResult { Status = status };
    }

    private sealed record OpenStream(MatchMetricStream Stream, MatchMetricsAdmit Admit, DateTime CheckedAt);
}
