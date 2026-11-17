namespace Infrastructure;

public interface IRuntimeChannelObserver : IGrainObserver
{
    Task Send(object message);
}

public class RuntimeChannelObserver : IRuntimeChannelObserver
{
    public RuntimeChannelObserver(Action<object> onMessage)
        : this(message => {
            onMessage(message);
            return Task.CompletedTask;
        })
    {
    }

    public RuntimeChannelObserver(Func<object, Task> onMessage)
    {
        _onMessage = onMessage;
    }

    private readonly Func<object, Task> _onMessage;
    private readonly SemaphoreSlim _deliveryGate = new(1, 1);
    private readonly object _bufferGate = new();

    private List<object> _bufferedMessages = new();
    private bool _isBuffering;

    // A delivery failed: the messages after it are not taken until a catch-up replays from the last seen sequence,
    // otherwise the last seen sequence would skip the failed one and the catch-up would not find it. Under _deliveryGate.
    private bool _needsCatchUp;

    public Guid Id { get; } = Guid.NewGuid();
    public long LastSeenSequence { get; private set; }

    // The channel activation LastSeenSequence belongs to (SequencedMessage.Epoch), empty before the first message.
    public Guid LastSeenEpoch { get; private set; }

    public void BeginBuffering()
    {
        lock (_bufferGate)
        {
            _bufferedMessages.Clear();
            _isBuffering = true;
        }
    }

    public async Task ReplayCatchUp(IReadOnlyList<SequencedMessage> messages)
    {
        await _deliveryGate.WaitAsync();

        try
        {
            foreach (var message in messages)
                await Deliver(message);

            _needsCatchUp = false;
        }
        finally
        {
            _deliveryGate.Release();
        }
    }

    public async Task EndBuffering()
    {
        List<object> bufferedMessages;

        lock (_bufferGate)
        {
            bufferedMessages = _bufferedMessages;
            _bufferedMessages = new List<object>();
            _isBuffering = false;
        }

        await _deliveryGate.WaitAsync();

        try
        {
            // The catch-up of the next resubscribe brings them again.
            if (_needsCatchUp)
                return;

            foreach (var message in bufferedMessages)
                await Deliver(message);
        }
        finally
        {
            _deliveryGate.Release();
        }
    }

    public void ResetLastSeen(Guid epoch, long lastSeenSequence)
    {
        LastSeenEpoch = epoch;
        LastSeenSequence = lastSeenSequence;
    }

    public async Task Send(object message)
    {
        lock (_bufferGate)
        {
            if (_isBuffering)
            {
                _bufferedMessages.Add(message);
                return;
            }
        }

        await _deliveryGate.WaitAsync();

        try
        {
            // Thrown to the grain: it drops the observer, the next resubscribe catches up.
            if (_needsCatchUp)
                throw new InvalidOperationException($"Channel observer {Id} waits for a catch-up after a failed delivery");

            await Deliver(message);
        }
        finally
        {
            _deliveryGate.Release();
        }
    }

    private async Task Deliver(object message)
    {
        try
        {
            await SendLocked(message);
        }
        catch
        {
            _needsCatchUp = true;
            throw;
        }
    }

    private async Task SendLocked(object message)
    {
        if (message is not SequencedMessage sequenced)
        {
            await _onMessage(message);
            return;
        }

        // The first message tells the epoch. A message of another epoch is a late one of a previous activation: the
        // switch to a new activation happens in the resubscribe, with its catch-up (RuntimeChannelResubscription).
        if (LastSeenSequence == 0 && LastSeenEpoch == Guid.Empty)
            LastSeenEpoch = sequenced.Epoch;

        if (sequenced.Epoch != LastSeenEpoch || sequenced.Sequence <= LastSeenSequence)
            return;

        await _onMessage(sequenced.Payload);
        LastSeenSequence = sequenced.Sequence;
    }
}
