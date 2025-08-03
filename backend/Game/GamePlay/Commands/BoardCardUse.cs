using Shared;

namespace Game.GamePlay;

public class BoardCardUse(GameCommandUtils utils) : GameCommand<SharedGameAction.UseBoardCard>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.UseBoardCard request)
    {
        var card = Utils.CardFactory.CreateBoard(context.Player, request.Type);
        return card.Use(request.Position);
    }
}