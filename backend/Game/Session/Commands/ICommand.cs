using Shared;

namespace Game;

public interface ICommand
{
    Type RequestType { get; }

    void Execute(CommandScope scope, INetworkContext context);
}