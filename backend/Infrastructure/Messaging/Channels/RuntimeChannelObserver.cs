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

    public Guid Id { get; } = Guid.NewGuid();
    public long LastSeenSequence { get; private set; }

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
                await SendLocked(message);
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
            foreach (var message in bufferedMessages)
                await SendLocked(message);
        }
        finally
        {
            _deliveryGate.Release();
        }
    }

    public void ResetLastSeen(long lastSeenSequence)
    {
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
            await SendLocked(message);
        }
        finally
        {
            _deliveryGate.Release();
        }
    }

    private async Task SendLocked(object message)
    {
        if (message is not SequencedMessage sequenced)
        {
            await _onMessage(message);
            return;
        }

        if (sequenced.Sequence <= LastSeenSequence)
            return;

        await _onMessage(sequenced.Payload);
        LastSeenSequence = sequenced.Sequence;
    }
}
