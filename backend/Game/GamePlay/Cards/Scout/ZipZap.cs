using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class ZipZap : ICard<CardUsePayload.ZipZap>
{
    public ZipZap(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult Use(CardUseContext context, CardUsePayload.ZipZap payload)
    {
        var invoker = context.Invoker;
        var snapshot = context.Snapshot;
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.ZipZap_Normal;
        var size = config.Size + (int)invoker.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);
        var searchShape = PatternShapes.Rhombus(config.SearchRadius);

        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No taken cells in the pattern")
            };
        }

        var targets = new List<ITakenCell>();
        var current = SelectTarget(payload.Position);

        if (current == null)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No target found in the pattern")
            };
        }

        targets.Add(current);

        for (var i = 1; i < size; i++)
        {
            current = SelectTarget(current.Position);

            if (current == null)
                break;

            targets.Add(current);
        }

        if (targets.Count == 0)
        {
            return new CardUseResult
            {
                Result = EmptyResponse.Fail("No targets found in the pattern")
            };
        }

        var targetPositions = targets.Select(t => t.Position).ToList();

        foreach (var target in targets)
            target.ToFree();

        var revealed = board.Revealer.Reveal(targetPositions);
        revealed.AddRange(targetPositions);
        revealed.AddRange(board.GetFreeNeighbours(targetPositions));

        var openedCells = revealed.Distinct().Select(p => new OpenedCell
        {
            Position = p,
            MinesAround = board.Cells[p].AsFree().MinesAround
        }).ToList();

        snapshot.RecordCardUse(invoker.User.Id, context.CardId, new CardActionSnapshot.ZipZap()
        {
            TargetPlayer = board.OwnerId,
            TargetCells = targetPositions,
            OpenedCells = openedCells
        });

        return new CardUseResult
        {
            Result = EmptyResponse.Ok
        };

        ITakenCell? SelectTarget(Position center)
        {
            var searchPositions = searchShape.SelectTaken(board, center);
            var hasMine = searchPositions.Where(x => x.HasMine == true);
            var hasFlags = hasMine.Where(x => x.IsFlagged == false);
            var unique = hasFlags.Where(x => targets.Contains(x) == false);

            var ordered = unique
                          .OrderBy(x => x.Position.DistanceTo(center))
                          .ToList();

            if (ordered.Count == 0)
                return null;

            return ordered.First();
        }
    }
}