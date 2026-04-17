using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

/// <summary>
/// Reveals a random number of hidden mines on the owner's field as temporary highlights without flagging them.
/// </summary>
public class FortuneCookie : ICard<CardUsePayload.FortuneCookie>
{
    public FortuneCookie(ICardConfigs configs, IGameRandom gameRandom, IRoundActionService roundActionService)
    {
        _configs = configs;
        _gameRandom = gameRandom;
        _roundActionService = roundActionService;
    }

    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;
    private readonly IRoundActionService _roundActionService;

    public CardUseResult Use(CardUseContext context, CardUsePayload.FortuneCookie payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;

        var mineCells = board.Cells.Values
                             .Where(c => c.Status == CellStatus.Taken)
                             .Select(c => c.ToTaken())
                             .Where(c => c.HasMine)
                             .ToList();

        if (mineCells.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No hidden mines on board")
            };
        }

        var config = _configs.Value.FortuneCookie_Normal;
        var count = _gameRandom.Range(invoker, config.MinMines, config.MaxMines);
        count = Math.Min(count, mineCells.Count);

        var effectId = Guid.NewGuid();
        var revealed = new List<Position>(count);
        var affectedCells = new List<ICell>();

        for (var i = 0; i < count; i++)
        {
            var index = _gameRandom.Index(invoker, mineCells.Count);
            var cell = mineCells[index];
            revealed.Add(cell.Position);
            cell.AddEffect(new MineHighlightEffect { Id = effectId });
            affectedCells.Add(cell);
            mineCells.RemoveAt(index);
        }

        if (affectedCells.Count > 0)
            _roundActionService.Schedule(new MineHighlightDisposeAction(board, effectId, affectedCells), 1);

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.FortuneCookie()
        {
            TargetPlayer = invoker.User.Id,
            RevealedMines = revealed,
            TargetCells = revealed,
            AffectedCells = affectedCells.Select(c => c.Position).ToArray(),
            EffectId = effectId
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };
    }
}