using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public class CardUseCommand(GameCommandUtils utils, ICardConfigs configs) : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        var player = context.Player;
        var handCard = player.Hand.Entries.FirstOrDefault(c => c.Id == request.CardId);

        if (handCard == null)
            return EmptyResponse.Fail($"Card {request.CardId} not found in hand");

        var config = configs.Value.All[handCard.Type];
        var manaCost = config.ManaCost;
        var nextDiscount = (int)player.Modifiers.Get(PlayerModifier.NextCardDiscount);
        var allDiscount = (int)player.Modifiers.Get(PlayerModifier.AllCardsDiscount);
        var penalty = (int)player.Modifiers.Get(PlayerModifier.ManaCostPenalty);

        if (nextDiscount > 0)
        {
            manaCost -= nextDiscount;
            player.Modifiers.Reset(context.Snapshot, PlayerModifier.NextCardDiscount);
        }

        manaCost -= allDiscount;
        manaCost += penalty;

        if (manaCost < 0)
            manaCost = 0;

        var cardContext = new CardUseContext
        {
            Invoker = player,
            Snapshot = context.Snapshot,
            CardId = request.CardId
        };

        var prefixMark = context.Snapshot.Count;

        var use = Utils.ServiceProvider.Use(cardContext, request.Payload);

        if (use.Result.HasError == true)
            return use.Result;

        using (context.Snapshot.BeginInsertAt(prefixMark))
        {
            player.Mana.Use(context.Snapshot, manaCost);
            player.Moves.OnUsed(context.Snapshot);
        }

        player.Hand.Remove(request.CardId);
        context.Snapshot.RecordCardRemove(player.User.Id, request.CardId);

        player.Stash.Add(handCard.Type);
        context.Snapshot.RecordCardAdd(player.User.Id, request.CardId, handCard.Type, isStash: true);
        context.Snapshot.RecordStashUpdate(player);

        context.Player.Actions.OnCardUsed(handCard.Type, request.Payload);

        Utils.SessionLogger.LogCardUsed(player.User.Id, handCard.Type, manaCost, use.Result.HasError == false);

        return use.Result;
    }
}
