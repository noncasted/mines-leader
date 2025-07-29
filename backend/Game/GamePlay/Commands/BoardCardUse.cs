using Context;
using Shared;

namespace Game.GamePlay;

public class BoardCardUse(GameCommandUtils utils) : GameCommand<SharedGameAction.UseBoardCard>(utils)
{
    protected override EmptyResponse Execute(IPlayer player, SharedGameAction.UseBoardCard request)
    {
        var target = SelectBoard();
        
        
        
        IBoard SelectBoard()
        {
            return request.Type switch
            {
                CardType.Trebuchet => Opponent(),
                CardType.Trebuchet_Max => Opponent(),
                CardType.Bloodhound => player.Board,
                CardType.Bloodhound_Max => player.Board,
                CardType.ErosionDozer => player.Board,
                CardType.ErosionDozer_Max => player.Board,
                CardType.ZipZap => player.Board,
                CardType.ZipZap_Max => player.Board,
                _ => throw new ArgumentOutOfRangeException()
            };

            IBoard Opponent()
            {
                return Utils.GameContext.GetOpponent(player).Board;
            }
        }
    }
}