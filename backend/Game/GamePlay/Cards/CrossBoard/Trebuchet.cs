using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class Trebuchet : ICard<CardUsePayload.Trebuchet>
{
    public Trebuchet(ICardConfigs configs, IGameContext gameContext)
    {
        _configs = configs;
        _gameContext = gameContext;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameContext _gameContext;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.Trebuchet payload)
    {
        var opponent = _gameContext.GetOpponent(invoker);
        var board = opponent.Board;
        board.EnsureGenerated(payload.Position);

        if (board.Cells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("TargetId board has no cells"),
                ActionData = null
            };
        }

        var config = _configs.Value.Trebuchet_Normal;
        var size = config.Size + (int)invoker.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);

        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No free cells in the pattern"),
                ActionData = null
            };
        }

        var minesTargets = new List<ICell>();

        var cellsByY = Enumerable.GroupBy<ICell, int>(selected, cell => cell.Position.y)
                                 .OrderByDescending(group => group.Key);

        foreach (var group in cellsByY)
        {
            if (group.Count() == 1)
            {
                minesTargets.Add(group.First());
            }
            else
            {
                minesTargets.Add(group.First());
                minesTargets.Add(group.Last());
            }
        }

        foreach (var cell in selected)
            cell.ToTaken();

        foreach (var cell in minesTargets)
            cell.ToTaken().SetMine();

        invoker.Modifiers.Reset(PlayerModifier.TrebuchetBoost);

        return new CardUseResult
        {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.Trebuchet()
            {
                TargetPlayer = board.OwnerId,
                TargetCells = selected.Select(c => c.Position).ToList()
            }
        };
    }
}