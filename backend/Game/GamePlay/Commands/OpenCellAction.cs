using Shared;

namespace Game.GamePlay;

public class OpenCellAction(GameCommandUtils utils) : GameCommand<SharedGameAction.Open>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.Open request)
    {
        var board = context.Player.Board;
        board.EnsureGenerated(request.Position);
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Failed;

        if (targetCell.Status == CellStatus.Taken)
        {
            context.Player.Health.TakeDamage(1);
            targetCell.ToTaken().Explode();
        }

        targetCell.ToFree();
        board.OnUpdated();

        return EmptyResponse.Ok;
    }
}