using Cluster.Configs;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class CardUseCommand(GameCommandUtils utils, ICardConfigs configs, MoveSnapshotAccessor snapshotAccessor)
    : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        var player = context.Player;
        var handCard = player.Hand.Entries.FirstOrDefault(c => c.Id == request.CardId);

        if (handCard == null)
            return EmptyResponse.Fail($"Card {request.CardId} not found in hand");

        Utils.Logger.LogInformation("[Game] [Command] Player {PlayerId} is using card {CardType}",
            context.Player.User.Id, handCard.Type);

        var config = configs.Value.All[handCard.Type];
        var manaCost = config.ManaCost;
        var nextDiscount = (int)player.Modifiers.Get(PlayerModifier.NextCardDiscount);
        var allDiscount = (int)player.Modifiers.Get(PlayerModifier.AllCardsDiscount);
        var penalty = (int)player.Modifiers.Get(PlayerModifier.ManaCostPenalty);

        if (nextDiscount > 0)
        {
            manaCost -= nextDiscount;
            player.Modifiers.Reset(PlayerModifier.NextCardDiscount);
        }

        manaCost -= allDiscount;
        manaCost += penalty;

        if (manaCost < 0)
            manaCost = 0;

        snapshotAccessor.Set(context.Snapshot, request.CardId);

        var use = Utils.ServiceProvider.Use(player, request.Payload);

        if (use.Result.HasError == true)
            return use.Result;

        player.Hand.Remove(request.CardId);

        if (use.ActionData != null)
            context.Snapshot.RecordCardUse(player.User.Id, request.CardId, use.ActionData);

        foreach (var (_, board) in Utils.GameContext.Boards)
            board.OnUpdated();

        player.Stash.Add(handCard.Type);
        player.Mana.Use(manaCost);
        player.Moves.OnUsed();
        context.Player.Actions.OnCardUsed(handCard.Type, request.Payload);

        Utils.SessionLogger.LogCardUsed(player.User.Id, handCard.Type, manaCost, use.Result.HasError == false);

        return use.Result;
    }
}