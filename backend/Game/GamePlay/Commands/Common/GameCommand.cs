using Common;
using Microsoft.Extensions.Logging;
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
        var player = Utils.GameContext.UserToPlayer[user];
        var lifetime = new Lifetime();

        var snapshot = new MoveSnapshot(Utils.GameContext, lifetime);
        snapshot.Start();

        var commandContext = new Context
        {
            Player = player,
            Lifetime = lifetime,
            Snapshot = snapshot
        };

        try
        {
            var response = Execute(commandContext, request);
            Utils.SnapshotSender.Send(snapshot);
            return response;
        }
        catch (Exception e)
        {
            Utils.Logger.LogError(e, "[Game] [Command] Error executing command {CommandName} for player {PlayerId}",
                request.GetType().Name, player.User.Id
            );
            return EmptyResponse.Fail("An error occurred while processing the command.");
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
        public required MoveSnapshot Snapshot { get; init; }
    }
}