using Shared;

namespace Game.GamePlay;

public class RemoveFlagAction(GameCommandUtils utils) : GameCommand<SharedGameAction.RemoveFlag>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.RemoveFlag request)
    {
        context.Snapshot.HandleBoards(context.Lifetime, Utils.GameContext);

        var board = context.Player.Board;
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Failed;

        if (targetCell.Effects.Any(e => e.Type == CellEffectType.Frost))
            return EmptyResponse.Fail("Cell is frozen");

        var taken = targetCell.ToTaken();

        if (taken.IsFlagged == false)
            return EmptyResponse.Failed;

        taken.RemoveFlag();
        board.OnUpdated();

        Utils.SessionLogger.LogFlagRemoved(context.Player.User.Id, request.Position);

        return EmptyResponse.Ok;
    }
}