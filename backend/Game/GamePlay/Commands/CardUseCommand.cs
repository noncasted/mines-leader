using Cluster.Configs;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class CardUseCommand(GameCommandUtils utils, ICardConfigs configs)
    : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        var player = context.Player;
        var handCard = player.Hand.Entries.FirstOrDefault(c => c.Id == request.CardId);

        if (handCard == null)
            return EmptyResponse.Fail($"Card {request.CardId} not found in hand");

        Utils.Logger.LogInformation("[Game] [Command] Player {PlayerId} is using card {CardType}",
            context.Player.User.Id, handCard.Type
        );

        var card = Utils.CardFactory.Create(player, context.Snapshot, request.Payload);

        var use = card.Use();

        if (use.Result.HasError == true)
            return use.Result;

        player.Hand.Remove(request.CardId);
        context.Snapshot.RecordCardUse(player.User.Id, request.CardId, use.ActionData!);

        foreach (var (_, board) in Utils.GameContext.Boards)
            board.OnUpdated();

        var config = configs.Value.All[handCard.Type];
        player.Stash.Add(handCard.Type);
        player.Mana.Use(config.ManaCost);
        player.Moves.OnUsed();
        context.Player.Actions.OnCardUsed();

        return use.Result;
    }
}