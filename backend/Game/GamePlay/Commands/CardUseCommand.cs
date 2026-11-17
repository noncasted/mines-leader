using Cluster.Configs;
using Game.Session;
using Shared;

namespace Game.GamePlay;

public class CardUseCommand(
    GameCommandUtils utils,
    ICardConfigs configs,
    IGameModeConfig modeConfigs,
    MatchCreateOptions matchOptions) : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        if (RequireOwnTurn(context) is { } refused)
            return refused;

        var player = context.Player;
        var handCard = player.Hand.Entries.FirstOrDefault(c => c.Id == request.CardId);

        if (handCard == null)
            return EmptyResponse.Fail($"Card {request.CardId} not found in hand");

        var config = configs.Value.All[handCard.Type];
        var opponent = Utils.GameContext.GetOpponent(player);

        // Доска противника появляется только после его первого хода: пока её нет, атакующие
        // карты сгенерировали бы её за него, поэтому отыгрывать их нельзя.
        if (config.Target == CardTarget.OpponentBoard && opponent.Board.IsGenerated == false)
            return EmptyResponse.Fail("Opponent board is not generated yet");

        var manaCost = CardManaCost.Resolve(player, config);

        // Mana.Use обрезает остаток до нуля: без этой проверки любая карта играется на пустом
        // пуле. UI клиента такую карту прячет, мост агента нет.
        if (player.Mana.Current < manaCost)
            return EmptyResponse.Fail(CardManaCost.NotEnough(manaCost, player.Mana.Current));

        if ((int)player.Modifiers.Get(PlayerModifier.NextCardDiscount) > 0)
            player.Modifiers.Reset(context.Snapshot, PlayerModifier.NextCardDiscount);

        var cardContext = new CardUseContext
        {
            Invoker = player,
            Snapshot = context.Snapshot,
            CardId = request.CardId
        };

        var opponentTakenBefore = CountTakenCells(opponent.Board);
        var modifiersBefore = player.Modifiers.Sources.Count;
        var opponentModifiersBefore = opponent.Modifiers.Sources.Count;

        context.Snapshot.HasDropPosition = request.HasDropPosition;
        context.Snapshot.DropX = request.DropX;
        context.Snapshot.DropY = request.DropY;

        var use = Utils.ServiceProvider.Use(cardContext, request.Payload);

        if (use.Result.HasError == true)
            return use.Result;

        player.Mana.Use(context.Snapshot, manaCost);
        player.Moves.OnUsed(context.Snapshot, modeConfigs.Value.GetCardMovesCost(matchOptions.Type));

        player.Hand.Remove(request.CardId);
        context.Snapshot.RecordCardRemove(player.User.Id, request.CardId);

        player.Stash.Add(handCard.Type);
        context.Snapshot.RecordCardAdd(player.User.Id, request.CardId, handCard.Type, isStash: true);
        context.Snapshot.RecordStashUpdate(player);

        context.Player.Actions.OnCardUsed(handCard.Type, request.Payload);

        Utils.SessionLogger.LogCardUsed(player.User.Id, handCard.Type, manaCost, use.Result.HasError == false);

        RecordStats(player, opponent, config, manaCost, opponentTakenBefore, modifiersBefore, opponentModifiersBefore);

        return use.Result;
    }

    private void RecordStats(
        IPlayer player,
        IPlayer opponent,
        ICardConfig config,
        int manaCost,
        int opponentTakenBefore,
        int modifiersBefore,
        int opponentModifiersBefore)
    {
        var userId = player.User.Id;
        var stats = Utils.Stats;

        stats.Add(userId, UserStatType.CardsPlayed);
        stats.AddCardPlayed(userId, config.Group);
        stats.Add(userId, UserStatType.ManaSpent, manaCost);

        if (config.Target == CardTarget.OpponentBoard || config.Target == CardTarget.Opponent)
            stats.Add(userId, UserStatType.CrossBoardCardsPlayed);

        var planted = CountTakenCells(opponent.Board) - opponentTakenBefore;

        if (planted > 0)
            stats.Add(userId, UserStatType.EnemyCellsPlanted, planted);

        var gained = player.Modifiers.Sources.Count - modifiersBefore;

        if (gained > 0)
            stats.Add(userId, UserStatType.BuffsReceived, gained);

        var opponentGained = opponent.Modifiers.Sources.Count - opponentModifiersBefore;

        if (opponentGained > 0)
            stats.Add(opponent.User.Id, UserStatType.DebuffsReceived, opponentGained);
    }

    private static int CountTakenCells(IBoard board)
    {
        var count = 0;

        foreach (var cell in board.Cells.Values)
        {
            if (cell.Status == CellStatus.Taken)
                count++;
        }

        return count;
    }
}