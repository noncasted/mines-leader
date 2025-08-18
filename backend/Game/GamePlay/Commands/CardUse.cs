using Shared;

namespace Game.GamePlay;

public class CardUse(GameCommandUtils utils) : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        var card = Utils.CardFactory.Create(context.Player, context.Snapshot, request.Payload);
        context.Player.Hand.Remove(request.Payload.Type);

        var result = card.Use();

        if (result.HasError == true)
            return result;

        foreach (var (_, board) in Utils.GameContext.Boards)
            board.OnUpdated();
        
        return result;
    }
}