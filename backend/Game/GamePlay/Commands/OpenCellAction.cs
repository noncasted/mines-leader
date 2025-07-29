using Shared;

namespace Game.GamePlay;

public class OpenCellAction(GameCommandUtils utils) : GameCommand<SharedGameAction.Open>(utils)
{
    protected override EmptyResponse Execute(IPlayer player, SharedGameAction.Open request)
    {
        var board = player.Board;
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Failed;

        if (targetCell.Status == CellStatus.Taken)
        {
            player.Health.TakeDamage(1);
            targetCell.ToTaken().Explode();
        }

        targetCell.ToFree();

        return EmptyResponse.Ok;
    }
}