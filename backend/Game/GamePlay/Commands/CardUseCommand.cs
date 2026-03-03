using Cluster.Configs;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class CardUseCommand
(GameCommandUtils utils,
    ICardConfigs configs) : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        Utils.Logger.LogInformation("[Game] [Command] Player {PlayerId} is using card {CardType}",
            context.Player.User.Id, request.Payload.Type
        );
        
        var player = context.Player;
        var card = Utils.CardFactory.Create(player, context.Snapshot, request.Payload);
        player.Hand.Remove(request.Payload.Type);

        var result = card.Use();

        if (result.HasError == true)
            return result;

        foreach (var (_, board) in Utils.GameContext.Boards)
            board.OnUpdated();

        var config = configs.Value.All[request.Payload.Type];
        player.Stash.Add(request.Payload.Type);
        player.Mana.Use(config.ManaCost);
        player.Moves.OnUsed();

        return result;
    }
}