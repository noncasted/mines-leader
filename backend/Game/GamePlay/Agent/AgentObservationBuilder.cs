using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public static class AgentObservationBuilder
{
    public static SharedAgentObservation Build(
        IGameContext context,
        Guid viewerId,
        IReadOnlyList<string> events,
        string trigger,
        bool oracle,
        bool hasError,
        string error)
    {
        return Build(context, viewerId, events, trigger, oracle, hasError, error, currentPlayerId: null, cardConfigs: null);
    }

    public static SharedAgentObservation Build(
        IGameContext context,
        Guid viewerId,
        IReadOnlyList<string> events,
        string trigger,
        bool oracle,
        bool hasError,
        string error,
        Guid? currentPlayerId,
        CardConfigOptions? cardConfigs)
    {
        var observation = new SharedAgentObservation
        {
            Trigger = trigger ?? string.Empty,
            HasError = hasError,
            Error = error ?? string.Empty,
            Events = events?.ToList() ?? new List<string>(),
            Self = AgentPlayerViews.CreateEmpty(),
            Opponent = AgentPlayerViews.CreateEmpty()
        };

        var self = FindPlayer(context, viewerId);
        if (self == null)
        {
            observation.HasError = true;
            if (string.IsNullOrEmpty(observation.Error))
                observation.Error = "Viewer not found";

            ApplyGameOver(observation, context, observation.Trigger);
            return observation;
        }

        var opponent = FindOpponent(context, self);

        observation.IsOwnTurn = currentPlayerId != null && currentPlayerId.Value == viewerId;
        observation.Self = AgentPlayerViews.Create(self, hideHand: false, oracle, cardConfigs);
        observation.Opponent = opponent != null
            ? AgentPlayerViews.Create(opponent, hideHand: oracle == false, oracle, cardConfigs)
            : AgentPlayerViews.CreateEmpty();

        ApplyGameOver(observation, context, observation.Trigger);
        return observation;
    }

    private static IPlayer? FindPlayer(IGameContext context, Guid viewerId)
    {
        foreach (var player in context.Players)
        {
            if (player.User.Id == viewerId)
                return player;
        }

        return null;
    }

    private static IPlayer? FindOpponent(IGameContext context, IPlayer self)
    {
        foreach (var player in context.Players)
        {
            if (player != self)
                return player;
        }

        return null;
    }

    private static void ApplyGameOver(SharedAgentObservation observation, IGameContext context, string trigger)
    {
        var dead = FindDeadPlayer(context);
        var disconnected = FindDisconnectedPlayer(context);
        var gameOver = trigger == "game_over" || dead != null || disconnected != null;

        observation.GameOver = gameOver;

        if (dead != null)
        {
            var opponent = FindOpponent(context, dead);
            observation.WinnerId = opponent?.User.Id ?? Guid.Empty;
            observation.WinReason = $"Player {dead.User.Id} health reached 0";
            return;
        }

        if (disconnected != null)
        {
            var opponent = FindOpponent(context, disconnected);
            observation.WinnerId = opponent?.User.Id ?? Guid.Empty;
            observation.WinReason = $"Player {disconnected.User.Id} disconnected";
            return;
        }

        if (observation.WinReason == null)
            observation.WinReason = string.Empty;
    }

    private static IPlayer? FindDeadPlayer(IGameContext context)
    {
        foreach (var player in context.Players)
        {
            if (player.Health != null && player.Health.Current == 0)
                return player;
        }

        return null;
    }

    private static IPlayer? FindDisconnectedPlayer(IGameContext context)
    {
        foreach (var player in context.Players)
        {
            if (player.User.Lifetime?.IsTerminated == true)
                return player;
        }

        return null;
    }
}
