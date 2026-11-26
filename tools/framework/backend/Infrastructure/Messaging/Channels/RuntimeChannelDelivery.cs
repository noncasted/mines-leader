using Microsoft.Extensions.Logging;

namespace Infrastructure;

// The delivery loop of one observer of a channel: sends its queue in order, the first send that fails or exceeds
// DeliveryTimeoutSeconds ends the loop and drops the observer. Runs on the scheduler of the grain.
internal class RuntimeChannelDelivery
{
    public RuntimeChannelDelivery(
        string channelName,
        IRuntimeChannelConfig config,
        ILogger logger,
        Action<RuntimeChannel.ObserverData> remove)
    {
        _channelName = channelName;
        _config = config;
        _logger = logger;
        _remove = remove;
    }

    private readonly string _channelName;
    private readonly IRuntimeChannelConfig _config;
    private readonly ILogger _logger;
    private readonly Action<RuntimeChannel.ObserverData> _remove;

    public async Task Run(RuntimeChannel.ObserverData data)
    {
        await foreach (var sequenced in data.Queue.Reader.ReadAllAsync())
        {
            if (await TrySend(data, sequenced) == true)
                continue;

            _remove(data);
            return;
        }
    }

    private async Task<bool> TrySend(RuntimeChannel.ObserverData data, SequencedMessage sequenced)
    {
        try
        {
            var timeout = TimeSpan.FromSeconds(_config.Value.DeliveryTimeoutSeconds);
            var deliveryTask = data.Observer.Send(sequenced);

            // Canceled after the send: the timer of every message would otherwise live for the whole timeout.
            using var timer = new CancellationTokenSource();

            if (await Task.WhenAny(deliveryTask, Task.Delay(timeout, timer.Token)) != deliveryTask)
            {
                RuntimeChannelTelemetry.DeliveryTimeout.Add(1);
                // The observer is dropped already.
                LateTasks.Observe(deliveryTask, _logger,
                    "[Messaging] [Channel] A timed out delivery on {ChannelName} to observer {ObserverId} failed later",
                    _channelName, data.Id);

                _logger.LogWarning(
                    "[Messaging] [Channel] Delivery timeout on {ChannelName} to observer {ObserverId} at sequence {Sequence}",
                    _channelName, data.Id, sequenced.Sequence);
                return false;
            }

            timer.Cancel();
            await deliveryTask;
            return true;
        }
        catch (Exception e)
        {
            RuntimeChannelTelemetry.DeliveryFailure.Add(1);

            _logger.LogError(e,
                "[Messaging] [Channel] Delivering message from {ChannelName} to observer {ObserverId} failed at sequence {Sequence}",
                _channelName, data.Id, sequenced.Sequence);
            return false;
        }
    }
}
