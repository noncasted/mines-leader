using Shared;
using Cluster.Configs;

namespace Game.GamePlay;

public class ZipZap : ICard<CardUsePayload.ZipZap> {
    public ZipZap(ICardConfigs configs, IMoveSnapshotAccessor snapshotAccessor) {
        _configs = configs;
        _snapshotAccessor = snapshotAccessor;
    }

    private readonly ICardConfigs _configs;
    private readonly IMoveSnapshotAccessor _snapshotAccessor;

    public CardUseResult Use(IPlayer invoker, CardUsePayload.ZipZap payload) {
        var board = invoker.Board;
        board.EnsureGenerated(payload.Position);

        var config = _configs.Value.ZipZap_Normal;
        var size = config.Size + (int)invoker.Modifiers.Values[PlayerModifier.TrebuchetBoost] * 2;
        var pattern = PatternShapes.Rhombus(size);
        var searchShape = PatternShapes.Rhombus(config.SearchRadius);

        var selected = pattern.SelectFree(board, payload.Position);

        if (selected.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No taken cells in the pattern"),
                ActionData = null
            };
        }

        var targets = new List<ITakenCell>();
        var current = SelectTarget(payload.Position);

        if (current == null) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No target found in the pattern"),
                ActionData = null
            };
        }

        targets.Add(current);

        for (var i = 1; i < size; i++) {
            current = SelectTarget(current.Position);

            if (current == null)
                break;

            targets.Add(current);
        }

        if (targets.Count == 0) {
            return new CardUseResult {
                Result = EmptyResponse.Fail("No targets found in the pattern"),
                ActionData = null
            };
        }

        var snapshot = _snapshotAccessor.Snapshot;
        snapshot.Lock();

        foreach (var target in targets)
            target.ToFree();

        snapshot.Unlock();
        board.OnUpdated();

        foreach (var target in targets)
            board.Revealer.Reveal(target.Position);

        board.OnUpdated();

        return new CardUseResult {
            Result = EmptyResponse.Ok,
            ActionData = new CardActionSnapshot.ZipZap() {
                TargetPlayer = board.OwnerId,
                Targets = targets.Select(t => t.Position).ToList()
            }
        };

        ITakenCell? SelectTarget(Position center) {
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
