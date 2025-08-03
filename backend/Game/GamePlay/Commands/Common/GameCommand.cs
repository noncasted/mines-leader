using Common;
using Shared;

namespace Game.GamePlay;

public abstract class GameCommand<TRequest> : ResponseCommand<TRequest, EmptyResponse> where TRequest : INetworkContext
{
    public GameCommand(GameCommandUtils utils)
    {
        Utils = utils;
    }

    public readonly GameCommandUtils Utils;

    protected override EmptyResponse Execute(IUser user, TRequest request)
    {
        var player = Utils.GameContext.Players[user];
        var lifetime = new Lifetime();

        var commandContext = new Context
        {
            Player = player,
            Lifetime = lifetime
        };

        var snapshot = new MoveSnapshot(Utils.GameContext);
        snapshot.Start(lifetime);
        
        try
        {
            var response = Execute(commandContext, request);
            Utils.SnapshotSender.Send(snapshot);
            return response;
        }
        finally
        {
            lifetime.Terminate();
        }
    }

    protected abstract EmptyResponse Execute(Context context, TRequest request);

    public class Context
    {
        public required IPlayer Player { get; init; }
        public required IReadOnlyLifetime Lifetime { get; init; }
    }
}