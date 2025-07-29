using Shared;

namespace Game.GamePlay;

public abstract class GameCommand<TRequest> : ResponseCommand<TRequest, EmptyResponse> where TRequest : INetworkContext
{
    public GameCommand(GameCommandUtils utils)
    {
        Utils = utils;
    }
    
    public readonly GameCommandUtils Utils;
    
    protected override EmptyResponse Execute(IUser user, TRequest context)
    {
        var player = Utils.GameContext.Players[user];
        return Execute(player, context);
    }

    protected abstract EmptyResponse Execute(IPlayer player, TRequest request);
}