using Cysharp.Threading.Tasks;
using Internal;

namespace Meta
{
    public interface IMetaConnection
    {
        // A lost meta socket (a dropped network, a restarted server) is opened again as the same user before a
        // request of the menu. Nothing is done while it is alive; concurrent callers share one reconnect. A failed
        // reconnect throws (the caller shows the loss; the next call tries again).
        UniTask EnsureConnected(IReadOnlyLifetime lifetime);
    }
}
