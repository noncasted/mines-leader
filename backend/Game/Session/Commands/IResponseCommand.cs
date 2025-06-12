using Shared;

namespace Game;

public interface IResponseCommand
{
    Type RequestType { get; }

    INetworkContext Execute(CommandScope scope, INetworkContext context);
}