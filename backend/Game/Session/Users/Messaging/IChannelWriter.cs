using Common;
using Shared;

namespace Game;

public interface IChannelWriter
{
    Task Run(IReadOnlyLifetime lifetime);
    ValueTask WriteEmpty(INetworkContext context);
    ValueTask WriteFull(INetworkContext context, int requestId);
}