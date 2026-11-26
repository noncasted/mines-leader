using Microsoft.Extensions.Logging;

namespace Infrastructure;

// Binds the observer to its channel again and brings it up to date with the catch-up of the channel.
internal class RuntimeChannelResubscription
{
    public RuntimeChannelResubscription(
        IRuntimeChannelId id,
        IRuntimeChannel channel,
        RuntimeChannelObserver observerSource,
        IRuntimeChannelObserver observerReference,
        RuntimeChannelGapHandlers gapHandlers,
        ILogger logger)
    {
        _id = id;
        _channel = channel;
        _observerSource = observerSource;
        _observerReference = observerReference;
        _gapHandlers = gapHandlers;
        _logger = logger;
    }

    private readonly IRuntimeChannelId _id;
    private readonly IRuntimeChannel _channel;
    private readonly RuntimeChannelObserver _observerSource;
    private readonly IRuntimeChannelObserver _observerReference;
    private readonly RuntimeChannelGapHandlers _gapHandlers;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _lock = new(1, 1);

    // One at a time: the first Listen and the resubscribe loop can run it together, and a second BeginBuffering
    // in the middle of the first one dropped the messages the first one had buffered.
    public async Task Run()
    {
        await _lock.WaitAsync();

        try
        {
            await RunLocked();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task RunLocked()
    {
        _observerSource.BeginBuffering();
        var lastSeenSequence = _observerSource.LastSeenSequence;
        var lastSeenEpoch = _observerSource.LastSeenEpoch;

        try
        {
            var position = await _channel.AddObserver(_observerSource.Id, _observerReference);

            // Nothing seen yet: only what comes after this bind is for the observer. A message that fails or is lost
            // from now on is found by the catch-up of the next resubscribe, the history before the bind is not.
            if (lastSeenSequence == 0 && lastSeenEpoch == Guid.Empty)
            {
                _observerSource.ResetLastSeen(position.Epoch, position.Sequence);
                return;
            }

            var catchUp = await _channel.CatchUp(lastSeenSequence);

            if (catchUp.Epoch != lastSeenEpoch)
            {
                // A new activation of the channel numbers from 1 again: everything it has is new here, and what
                // the previous one published after lastSeenSequence is lost.
                _logger.LogWarning(
                    "[Messaging] [Channel] Channel {ChannelId} reactivated, last seen seq {LastSeq} of the previous activation, current seq {CurrentSeq}",
                    _id.ToRaw(), lastSeenSequence, catchUp.CurrentSequence);

                _observerSource.ResetLastSeen(catchUp.Epoch, 0);
                catchUp = await _channel.CatchUp(0);

                if (catchUp.Epoch != _observerSource.LastSeenEpoch)
                    throw new InvalidOperationException($"Channel {_id.ToRaw()} reactivated again during the catch-up");

                _gapHandlers.Invoke();
            }
            else if (catchUp.GapDetected)
            {
                _logger.LogWarning(
                    "[Messaging] [Channel] Gap detected on {ChannelId}, last seen seq {LastSeq}, current seq {CurrentSeq}, replaying {Count} messages",
                    _id.ToRaw(), lastSeenSequence, catchUp.CurrentSequence, catchUp.Messages.Count);

                _gapHandlers.Invoke();
            }

            await _observerSource.ReplayCatchUp(catchUp.Messages);
        }
        finally
        {
            await _observerSource.EndBuffering();
        }
    }
}
