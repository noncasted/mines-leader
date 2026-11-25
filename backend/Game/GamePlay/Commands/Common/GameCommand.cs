using Internal;
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

    protected virtual bool RequestOracle => false;

    protected override EmptyResponse Execute(IUser user, TRequest request)
    {
        var player = Utils.GameContext.UserToPlayer[user];
        var lifetime = new Lifetime();

        var snapshot = new MoveSnapshot(Utils.SessionLogger);

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

                // The agent waits for an observation frame after every command; without publishing it gets
                // "Timed out waiting for observation" instead of the error text.
                var failed = EmptyResponse.Fail("An error occurred while processing the command.");
                Utils.ObservationPublisher?.Publish(player.User.Id, "action", true, failed.Message, RequestOracle);
                return failed;
            }

            if (preState != null)
            {
                var postState = GameStateCapture.Capture(Utils.GameContext);

                Utils.DiffGuard.Validate(preState, snapshot.Collect(), postState,
                    $"command:{request.GetType().Name}");
            }

            Utils.SnapshotSender.Send(snapshot);
            Utils.ObservationPublisher?.Publish(
                player.User.Id,
                "action",
                response.HasError,
                response.Message,
                RequestOracle);
            return response;
        }
        finally
        {
            lifetime.Terminate();
        }
    }

    protected abstract EmptyResponse Execute(Context context, TRequest request);

    /// <summary>
    /// Turn queue for board actions and cards. In turn-based mode CurrentPlayer is set,
    /// in real time it is null and the queue is not checked. Previously an action out of turn
    /// reached Moves.OnUsed, threw an exception and went to the client as
    /// "An error occurred while processing the command".
    /// </summary>
    protected EmptyResponse? RequireOwnTurn(Context context)
    {
        var current = Utils.GameRound.CurrentPlayer?.Value;

        if (current != null && ReferenceEquals(current, context.Player) == false)
            return EmptyResponse.Fail("Not your turn");

        return null;
    }

    /// <summary>
    /// Turn queue plus remaining moves: open and chord cost a move, without one the action is refused before
    /// any side effects (board generation, log, damage).
    /// </summary>
    protected EmptyResponse? RequireMove(Context context)
    {
        var refused = RequireOwnTurn(context);

        if (refused != null)
            return refused;

        if (context.Player.Moves.Left <= 0)
            return EmptyResponse.Fail("No moves left");

        return null;
    }

    public class Context
    {
        public required IPlayer Player { get; init; }
        public required IReadOnlyLifetime Lifetime { get; init; }
        public required MoveSnapshot Snapshot { get; init; }
    }
}