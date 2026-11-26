using System.Collections.Concurrent;
using Shared;

namespace Framework.ClientTelemetry.TestHost;

// The messages that really came through the sockets of this Kestrel: the proof of the network, without their contents.
public sealed class TelemetryHostStats
{
    private readonly ConcurrentDictionary<string, long> _messages = new();
    private long _total;

    public long Total => Interlocked.Read(ref _total);
    public IReadOnlyDictionary<string, long> Messages => _messages.ToDictionary(pair => pair.Key, pair => pair.Value);

    public void Count(INetworkContext message)
    {
        Interlocked.Increment(ref _total);
        _messages.AddOrUpdate(message.GetType().Name, 1, (_, count) => count + 1);
    }
}
