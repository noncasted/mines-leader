using Common.Reactive;
using Game.GamePlay.Snapshots;
using Game.Session;
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

        var snapshot = new MoveSnapshot();

        var commandContext = new Context
        {
            Player = player,
            Lifetime = lifetime,
            Snapshot = snapshot
        };

        var preState = Utils.DiffGuard.IsEnabled == true
            ? GameStateCapture.Capture(Utils.GameContext)
            : null;

        try
        {
            EmptyResponse response;

            try
            {
                response = Execute(commandContext, request);
            }
            catch (Exception e)
            {
                Utils.Logger.LogError(e, "[Game] [Command] Error executing command {CommandName} for player {PlayerId}",
                    request.GetType().Name, player.User.Id);
                return EmptyResponse.Fail("An error occurred while processing the command.");
            }

            if (preState != null)
            {
                var postState = GameStateCapture.Capture(Utils.GameContext);

                Utils.DiffGuard.Validate(preState, snapshot.Collect(), postState,
                    $"command:{request.GetType().Name}");
            }

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
        public required MoveSnapshot Snapshot { get; init; }
    }
}