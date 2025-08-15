using Shared;

namespace Game.GamePlay;

public class CardUse(GameCommandUtils utils) : GameCommand<SharedGameAction.CardUse>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.CardUse request)
    {
        var card = Utils.CardFactory.Create(context.Player, request.Payload);
        context.Player.Hand.Remove(request.Payload.Type);
        return card.Use();
    }
}