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
            view.ManaCost = CardManaCost.Resolve(self, config);

            // Пока противник не сделал первый ход, его доски нет и карты по ней запрещены.
            if (config.Target == CardTarget.OpponentBoard && opponent?.Board.IsGenerated == false)
            {
                view.Error = "Opponent board is not generated yet";
                return view;
            }

            if (self.Mana.Current < view.ManaCost)
            {
                view.Error = CardManaCost.NotEnough(view.ManaCost, self.Mana.Current);
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
        // CardUsePayload.ZipZap.CardId — это id самой карты, сервер его не читает (ZipZap.Use берёт
        // context.CardId). Дополнительная карта нужна только Recycler.
        view.NeedsExtraCard = payload is CardUsePayload.Recycler;
        view.NeedsChosenIndex = payload is CardUsePayload.Salvage;

        if (view.NeedsExtraCard)
            view.ExtraCardIds = ExtraCardIds(self.Hand, card.Id);

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
            view.Error = "Opponent board is not generated yet";
            return;
        }

        var info = AgentCardCatalog.Get(type);
        if (info.Shape == AgentCardCatalog.ShapeNone)
        {
            view.Error = $"No target rule for {type}";
            return;
        }

        var width = board.Size.x;
        var height = board.Size.y;

        // Своя доска появляется на первом клике, и карты по ней зовут EnsureGenerated сами:
        // до генерации легальна любая клетка. Исключение — карты, которым нужны открытые
        // клетки (ZipZap): на свежей доске их нет. Пустой список без ошибки агент читает
        // как "нельзя", поэтому оба случая отвечаем явно.
        if (board.IsGenerated == false)
        {
            if (info.Cells == AgentCardCells.Free)
            {
                view.Error = "Own board is not generated yet: open a cell first";
                return;
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    view.Cells.Add(new AgentLegalCell { X = x, Y = y });
            }

            return;
        }

        ICardConfig? config = null;
        cardConfigs?.All.TryGetValue(type, out config);
        var size = AgentCardCatalog.ResolveSize(type, config);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var position = new Position(x, y);
                if (IsLegal(info, size, board, position))
                    view.Cells.Add(new AgentLegalCell { X = x, Y = y });
            }
        }
    }

    /// <summary>
    /// Повторяет фильтр клеток из Use карты по видимому состоянию доски. Мины не учитываются:
    /// карты вроде ChainReaction, которым нужна мина под кликом, получают все закрытые клетки.
    /// </summary>
    private static bool IsLegal(AgentCardInfo info, int size, IBoard board, Position position)
    {
        switch (info.Shape)
        {
            case AgentCardCatalog.ShapeRhombus:
                return Matches(PatternShapes.Rhombus(size), info.Cells, board, position);
            case AgentCardCatalog.ShapeCross:
                return Matches(PatternShapes.Cross(size), info.Cells, board, position);
            case AgentCardCatalog.ShapeLine:
                // Карта берёт ориентацию с большим числом подходящих клеток (MinefieldScout.Use),
                // поэтому позиция легальна, если хотя бы одна ориентация что-то задевает.
                return Matches(PatternShapes.Line(size, horizontal: true), info.Cells, board, position)
                       || Matches(PatternShapes.Line(size, horizontal: false), info.Cells, board, position);
            case AgentCardCatalog.ShapeSingle:
                return board.Cells.TryGetValue(position, out var single)
                       && single.IsTaken()
                       && single.AsTaken().IsFlagged == false;
            case AgentCardCatalog.ShapeChain:
                return board.Cells.TryGetValue(position, out var chain) && chain.IsTaken();
            default:
                return false;
        }
    }

    private static bool Matches(IPattenShape pattern, AgentCardCells cells, IBoard board, Position position)
    {
        return cells switch
        {
            AgentCardCells.Taken => pattern.SelectTaken(board, position).Count > 0,
            AgentCardCells.Free => pattern.SelectFree(board, position).Count > 0,
            _ => pattern.SelectAll(board, position).Count > 0,
        };
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

    private static List<Guid> ExtraCardIds(IHand hand, Guid selfId)
    {
        var ids = new List<Guid>();

        foreach (var card in hand.Entries)
        {
            if (card.Id == selfId)
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
