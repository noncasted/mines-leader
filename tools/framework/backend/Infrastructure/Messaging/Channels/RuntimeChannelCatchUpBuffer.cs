using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// The numbering of the messages of one channel activation and the ring of the last ones a client catches up from.
internal class RuntimeChannelCatchUpBuffer
{
    public RuntimeChannelCatchUpBuffer(int size, string channelName, ILogger logger)
    {
        _size = size;
        _buffer = new SequencedMessage?[size];
        _channelName = channelName;
        _logger = logger;
    }

    private readonly int _size;
    private readonly SequencedMessage?[] _buffer;
    private readonly string _channelName;
    private readonly ILogger _logger;

    // The sequence numbers live in this activation only: the epoch tells a client that saw the numbers of another
    // activation that its LastSeenSequence means nothing here (RuntimeChannelResubscription).
    private readonly Guid _epoch = Guid.NewGuid();

    private long _sequenceNumber;

    public ChannelPosition Position => new() { Sequence = _sequenceNumber, Epoch = _epoch };

    public SequencedMessage Add(object message)
    {
        _sequenceNumber++;
        var sequenced = new SequencedMessage { Sequence = _sequenceNumber, Payload = message, Epoch = _epoch };
        _buffer[_sequenceNumber % _size] = sequenced;
        return sequenced;
    }

    public CatchUpResult CatchUp(long lastSeenSequence)
    {
        if (lastSeenSequence > _sequenceNumber)
        {
            RuntimeChannelTelemetry.GapDetected.Add(1);

            _logger.LogWarning(
                "[Messaging] [Channel] Sequence reset/gap detected on {ChannelName}: requested seq {RequestedSeq}, current seq {CurrentSeq}",
                _channelName, lastSeenSequence, _sequenceNumber);

            return new CatchUpResult
            {
                GapDetected = true,
                CurrentSequence = _sequenceNumber,
                Epoch = _epoch
            };
        }

        if (lastSeenSequence == _sequenceNumber)
        {
            return new CatchUpResult
            {
                CurrentSequence = _sequenceNumber,
                Epoch = _epoch
            };
        }

        var oldestInBuffer = _sequenceNumber - _size + 1;

        if (oldestInBuffer < 1)
            oldestInBuffer = 1;

        var gapDetected = lastSeenSequence < oldestInBuffer - 1;
        var startSequence = Math.Max(lastSeenSequence + 1, oldestInBuffer);

        var messages = new List<SequencedMessage>();

        for (var seq = startSequence; seq <= _sequenceNumber; seq++)
        {
            var entry = _buffer[seq % _size];

            if (entry != null && entry.Sequence == seq)
                messages.Add(entry);
        }

        if (messages.Count > 0)
        {
            RuntimeChannelTelemetry.CatchUpExecuted.Add(1);
            RuntimeChannelTelemetry.CatchUpMessages.Record(messages.Count);
        }

        if (gapDetected)
        {
            RuntimeChannelTelemetry.GapDetected.Add(1);

            _logger.LogWarning(
                "[Messaging] [Channel] Gap detected on {ChannelName}: requested seq {RequestedSeq}, oldest available {OldestSeq}, current seq {CurrentSeq}",
                _channelName, lastSeenSequence, oldestInBuffer, _sequenceNumber);
        }

        return new CatchUpResult
        {
            Messages = messages,
            GapDetected = gapDetected,
            CurrentSequence = _sequenceNumber,
            Epoch = _epoch
        };
    }
}
