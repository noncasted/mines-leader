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
            Self = CreateEmptyView(),
            Opponent = CreateEmptyView()
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
        observation.Self = CreatePlayerView(self, hideHand: false, oracle, cardConfigs);
        observation.Opponent = opponent != null
            ? CreatePlayerView(opponent, hideHand: oracle == false, oracle, cardConfigs)
            : CreateEmptyView();

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

    private static AgentPlayerView CreateEmptyView()
    {
        return new AgentPlayerView
        {
            Modifiers = new List<string>(),
            Hand = new List<AgentCardView>(),
            Cells = new List<AgentCellView>(),
            BoardAscii = string.Empty
        };
    }

    private static AgentPlayerView CreatePlayerView(
        IPlayer player,
        bool hideHand,
        bool oracle,
        CardConfigOptions? cardConfigs)
    {
        var view = new AgentPlayerView
        {
            Id = player.User.Id,
            Health = player.Health?.Current ?? 0,
            HealthMax = player.Health?.ResultMax ?? 0,
            Mana = player.Mana?.Current ?? 0,
            ManaMax = player.Mana?.ResultMax ?? 0,
            MovesLeft = player.Moves?.Left ?? 0,
            MovesMax = player.Moves?.ResultMax ?? 0,
            MovesAvailable = player.Moves?.IsAvailable ?? false,
            DeckCount = player.Deck?.Count ?? 0,
            StashCount = player.Stash?.Count ?? 0,
            Modifiers = CollectModifiers(player.Modifiers),
            Hand = CollectHand(player, hideHand, cardConfigs)
        };

        FillBoard(view, player.Board, oracle);
        return view;
    }

    private static List<string> CollectModifiers(IModifiers? modifiers)
    {
        var result = new List<string>();

        if (modifiers == null)
            return result;

        if (modifiers.Sources != null)
        {
            foreach (var source in modifiers.Sources)
            {
                if (source.Value != 0)
                    result.Add(source.Type.ToString());
            }
        }

        if (result.Count == 0 && modifiers.Values != null)
        {
            foreach (var (type, value) in modifiers.Values)
            {
                if (value != 0)
                    result.Add(type.ToString());
            }
        }

        return result;
    }

    private static List<AgentCardView> CollectHand(IPlayer player, bool hideHand, CardConfigOptions? cardConfigs)
    {
        var result = new List<AgentCardView>();
        var hand = player.Hand;

        if (hand?.Entries == null)
            return result;

        var configs = cardConfigs?.All;

        foreach (var card in hand.Entries)
        {
            if (hideHand)
            {
                result.Add(new AgentCardView
                {
                    Id = card.Id,
                    Type = "?",
                    ManaCost = 0
                });
                continue;
            }

            ICardConfig? config = null;
            configs?.TryGetValue(card.Type, out config);

            var info = AgentCardCatalog.Get(card.Type);

            result.Add(new AgentCardView
            {
                Id = card.Id,
                Type = card.Type.ToString(),
                ManaCost = config == null ? 0 : CardManaCost.Resolve(player, config),
                Target = (config?.Target ?? CardTarget.Self).ToString(),
                Shape = info.Shape,
                Size = AgentCardCatalog.ResolveSize(card.Type, config),
                NeedsPosition = CardUsePayloadFactory.CreateDefault(card.Type) is IBoardCardUsePayload,
                Summary = info.Summary
            });
        }

        return result;
    }

    private static void FillBoard(AgentPlayerView view, IBoard? board, bool oracle)
    {
        view.Cells = new List<AgentCellView>();
        view.BoardAscii = string.Empty;
        view.Mines = 0;
        view.Flags = 0;

        if (board == null)
            return;

        var width = board.Size.x;
        var height = board.Size.y;
        var lines = new List<string>(height);

        for (var y = 0; y < height; y++)
        {
            var chars = new char[width];

            for (var x = 0; x < width; x++)
            {
                var position = new Position(x, y);
                if (board.Cells.TryGetValue(position, out var cell) == false)
                {
                    chars[x] = '.';
                    continue;
                }

                var cellView = CreateCellView(cell, oracle);
                view.Cells.Add(cellView);
                chars[x] = ToAscii(cellView);
            }

            lines.Add(new string(chars));
        }

        view.BoardAscii = string.Join("\n", lines);

        if (board.MinesScanner != null)
        {
            view.Mines = board.MinesScanner.Mines + board.DetonatedMines;
            view.Flags = board.MinesScanner.Flags;
            return;
        }

        foreach (var cellView in view.Cells)
        {
            if (cellView.Status == "flagged")
                view.Flags++;

            if (cellView.HasMine || cellView.Status == "exploded")
                view.Mines++;
        }

        view.Mines += board.DetonatedMines;
    }

    private static AgentCellView CreateCellView(ICell cell, bool oracle)
    {
        var effects = new List<string>();
        foreach (var effect in cell.Effects)
            effects.Add(effect.Type.ToString());

        var view = new AgentCellView
        {
            X = cell.Position.x,
            Y = cell.Position.y,
            Effects = effects,
            Status = "closed"
        };

        if (cell.Status == CellStatus.Free)
        {
            view.Status = "open";
            view.MinesAround = cell.AsFree().MinesAround;
            return view;
        }

        var taken = cell.AsTaken();
        if (taken.IsFlagged)
        {
            view.Status = "flagged";
        }
        else
        {
            view.Status = "closed";
        }

        if (oracle || view.Status == "exploded")
            view.HasMine = taken.HasMine;

        return view;
    }

    private static char ToAscii(AgentCellView cell)
    {
        if (cell.Status == "exploded")
            return '*';

        if (cell.Status == "open")
            return (char)('0' + Math.Clamp(cell.MinesAround, 0, 8));

        if (cell.Status == "flagged")
            return 'F';

        foreach (var effect in cell.Effects)
        {
            if (effect == nameof(CellEffectType.Fog) || effect == nameof(CellEffectType.Blackout))
                return '~';
        }

        return '.';
    }
}
