using Shared;

namespace Game.GamePlay;

public static class AgentLegalPlaysBuilder
{
    public static SharedAgentLegalPlaysResponse Build(
        IGameContext context,
        Guid viewerId,
        Guid? currentPlayerId,
        CardConfigOptions? cardConfigs)
    {
        var response = new SharedAgentLegalPlaysResponse
        {
            IsOwnTurn = currentPlayerId != null && currentPlayerId.Value == viewerId
        };

        var self = FindPlayer(context, viewerId);
        if (self == null)
            return response;

        var opponent = FindOpponent(context, self);
        var entries = self.Hand?.Entries;
        if (entries == null)
            return response;

        foreach (var card in entries)
        {
            var view = BuildCard(card, self, opponent, cardConfigs);
            response.Cards.Add(view);
        }

        return response;
    }

    private static AgentLegalCardView BuildCard(
        ActiveCard card,
        IPlayer self,
        IPlayer? opponent,
        CardConfigOptions? cardConfigs)
    {
        var view = new AgentLegalCardView
        {
            Id = card.Id,
            Type = card.Type.ToString()
        };

        if (cardConfigs != null && cardConfigs.All.TryGetValue(card.Type, out var config))
        {
            view.ManaCost = config.ManaCost;

            // Пока противник не сделал первый ход, его доски нет и карты по ней запрещены.
            if (config.Target == CardTarget.OpponentBoard && opponent?.Board.IsGenerated == false)
            {
                view.Error = "Opponent board is not generated yet";
                return view;
            }
        }

        ICardUsePayload payload;
        try
        {
            payload = CardUsePayloadFactory.CreateDefault(card.Type);
        }
        catch (ArgumentException exception)
        {
            view.Error = exception.Message;
            return view;
        }

        view.NeedsPosition = payload is IBoardCardUsePayload;
        view.NeedsExtraCard = payload is CardUsePayload.Recycler || payload is CardUsePayload.ZipZap;
        view.NeedsChosenIndex = payload is CardUsePayload.Salvage;

        if (view.NeedsExtraCard)
            view.ExtraCardIds = ExtraCardIds(self.Hand, card.Id, payload is CardUsePayload.ZipZap);

        if (view.NeedsPosition == false)
            return view;

        FillBoardCells(view, card.Type, self, opponent, cardConfigs);
        return view;
    }

    private static void FillBoardCells(
        AgentLegalCardView view,
        CardType type,
        IPlayer self,
        IPlayer? opponent,
        CardConfigOptions? cardConfigs)
    {
        var board = TargetBoard(type, self, opponent, cardConfigs);
        if (board == null)
        {
            view.Error = $"Legal plays not implemented for {type}";
            return;
        }

        switch (type)
        {
            case CardType.Bloodhound:
            case CardType.Bloodhound_Max:
                FillBloodhound(view, type, board, cardConfigs);
                break;
            default:
                view.Error = $"Legal plays not implemented for {type}";
                break;
        }
    }

    private static void FillBloodhound(
        AgentLegalCardView view,
        CardType type,
        IBoard board,
        CardConfigOptions? cardConfigs)
    {
        if (cardConfigs == null)
        {
            view.Error = $"Legal plays not implemented for {type}";
            return;
        }

        var size = type == CardType.Bloodhound_Max
            ? cardConfigs.BloodHound_Max.Size
            : cardConfigs.BloodHound_Normal.Size;
        var pattern = PatternShapes.Rhombus(size);
        var width = board.Size.x;
        var height = board.Size.y;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var position = new Position(x, y);
                if (pattern.SelectTaken(board, position).Count > 0)
                    view.Cells.Add(new AgentLegalCell { X = x, Y = y });
            }
        }
    }

    private static IBoard? TargetBoard(
        CardType type,
        IPlayer self,
        IPlayer? opponent,
        CardConfigOptions? cardConfigs)
    {
        if (cardConfigs == null || cardConfigs.All.TryGetValue(type, out var config) == false)
            return self.Board;

        if (config.Target == CardTarget.OpponentBoard)
            return opponent?.Board;

        return self.Board;
    }

    private static List<Guid> ExtraCardIds(IHand hand, Guid selfId, bool includeSelf)
    {
        var ids = new List<Guid>();

        foreach (var card in hand.Entries)
        {
            if (includeSelf == false && card.Id == selfId)
                continue;

            ids.Add(card.Id);
        }

        return ids;
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
}
