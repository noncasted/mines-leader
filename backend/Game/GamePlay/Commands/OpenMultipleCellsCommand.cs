using Shared;

namespace Game.GamePlay;

public class OpenMultipleCellsCommand(GameCommandUtils utils) : GameCommand<SharedGameAction.OpenMultiple>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.OpenMultiple request)
    {
        var board = context.Player.Board;
        board.EnsureGenerated(request.Position);
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status != CellStatus.Free)
            return EmptyResponse.Failed;

        var free = targetCell.AsFree();
        var around = free.MinesAround;
        var placedFlags = 0;
        var takenNeighbours = new List<ITakenCell>();
        var flaggedNeighbours = new List<ITakenCell>();

        board.IterateNeighbours(request.Position, neighbour => {
            var neighbourCell = board.Cells[neighbour];

            if (neighbourCell.Status != CellStatus.Taken)
                return;

            var takenNeighbourCell = neighbourCell.AsTaken();
            takenNeighbours.Add(takenNeighbourCell);

            if (takenNeighbourCell.IsFlagged == false)
                return;

            flaggedNeighbours.Add(takenNeighbourCell);
            placedFlags++;
        });

        if (around != placedFlags)
            return EmptyResponse.Ok;

        var toReveal = new List<Position>();

        foreach (var neighbour in takenNeighbours)
        {
            if (flaggedNeighbours.Contains(neighbour) == true)
                continue;

            if (neighbour.HasMine == true)
            {
                context.Player.Health.TakeDamage(context.Snapshot, 1);

                context.Snapshot.RecordExplosion(board, neighbour.Position);
                neighbour.Explode();
                neighbour.ToFree();
                context.Snapshot.RecordCellFree(board, neighbour.Position);
                continue;
            }

            toReveal.Add(neighbour.Position);
        }

        toReveal.Add(request.Position);

        context.Player.Moves.OnUsed(context.Snapshot);

        context.Snapshot.RecordReveal(board, toReveal);

        return EmptyResponse.Ok;
    }
}