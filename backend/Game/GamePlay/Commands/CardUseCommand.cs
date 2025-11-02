using Shared;

namespace Game.GamePlay;

public class CardUseCommand(GameCommandUtils utils) : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        var player = context.Player;
        var card = Utils.CardFactory.Create(player, context.Snapshot, request.Payload);
        player.Hand.Remove(request.Payload.Type);

        var result = card.Use();

        if (result.HasError == true)
            return result;

        foreach (var (_, board) in Utils.GameContext.Boards)
            board.OnUpdated();

        var config = request.Payload.Type.ToConfig();
        player.Stash.Add(request.Payload.Type);
        player.Mana.Use(config.ManaCost);
        player.Moves.OnUsed();

        return result;
    }
}