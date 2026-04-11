using Shared;

namespace Game.GamePlay;

public class OpenCellCommand(GameCommandUtils utils) : GameCommand<SharedGameAction.Open>(utils)
{
    protected override EmptyResponse Execute(Context context, SharedGameAction.Open request)
    {
        var board = context.Player.Board;
        board.EnsureGenerated(request.Position);
        var targetCell = board.Cells[request.Position];

        if (targetCell.Status == CellStatus.Free)
            return EmptyResponse.Failed;

        if (targetCell.Effects.Any(e => e.Type == CellEffectType.Frost))
            return EmptyResponse.Fail("Cell is frozen");

        if (targetCell.ToTaken().HasMine == true)
        {
            var shield = (int)context.Player.Modifiers.Get(PlayerModifier.Shield);
            if (shield > 0) {
                context.Player.Modifiers.Set(PlayerModifier.Shield, shield - 1);
            } else {
                context.Player.Health.TakeDamage(1);
            }
            targetCell.ToTaken().Explode();
        }

        context.Player.Actions.OnCellOpened();
        context.Player.Moves.OnUsed();
        targetCell.ToFree();
        board.Revealer.Reveal(request.Position);
        board.OnUpdated();

        return EmptyResponse.Ok;
    }
}