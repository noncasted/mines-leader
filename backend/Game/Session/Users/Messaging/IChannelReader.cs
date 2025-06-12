using System.Threading.Channels;
using Common;
using Shared;

namespace Game;

public interface IChannelReader
{
    Channel<IServerRequest> Queue { get; }

    Task Run(ILifetime lifetime);
}