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

    protected virtual bool RequestOracle => false;

    protected override EmptyResponse Execute(IUser user, TRequest request)
    {
        var player = Utils.GameContext.UserToPlayer[user];
        var lifetime = new Lifetime();

        var snapshot = new MoveSnapshot
        {
            SessionLogger = Utils.SessionLogger
        };

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

                // Агент ждёт кадр наблюдения после каждой команды; без публикации он получает
                // "Timed out waiting for observation" вместо текста ошибки.
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
    /// Очередь хода для действий на доске и карт. В пошаговом режиме CurrentPlayer задан,
    /// в реальном времени он null и очередь не проверяется. Раньше действие вне хода
    /// доходило до Moves.OnUsed, падало с исключением и уходило клиенту как
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
    /// Очередь хода плюс остаток ходов: open и chord стоят ход, без него отказ до
    /// любых побочных эффектов (генерация доски, лог, урон).
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