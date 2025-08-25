using Shared;

namespace Game.GamePlay;

public class SkipTurn(GameCommandUtils utils) : GameCommand<SharedGameAction.SkipTurn>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.SkipTurn request)
    {
        if (Utils.GameRound.CurrentPlayer != context.Player)
            return EmptyResponse.Fail("Not your turn");

        Utils.GameRound.SkipTurn();
        
        return EmptyResponse.Ok;
    }
}