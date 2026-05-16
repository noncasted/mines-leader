using System.Text;
using Common;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay.Snapshots;

public interface ISnapshotDiffGuard
{
    bool IsEnabled { get; }
    void Validate(GameStateSnapshot pre, SharedMoveSnapshot records, GameStateSnapshot post, string contextTag);
}

public class SnapshotDiffGuard : ISnapshotDiffGuard
{
    public SnapshotDiffGuard(IClusterFlags flags, ILogger<SnapshotDiffGuard> logger)
    {
        _flags = flags;
        _logger = logger;
    }

    private readonly IClusterFlags _flags;
    private readonly ILogger<SnapshotDiffGuard> _logger;

    public bool IsEnabled => _flags.SnapshotDiffGuardEnabled;

    public void Validate(GameStateSnapshot pre, SharedMoveSnapshot records, GameStateSnapshot post, string contextTag)
    {
        if (IsEnabled == false)
            return;

        var expected = SnapshotApplier.Apply(pre, records);
        var diff = SnapshotDiffCalculator.Compute(expected, post);

        if (diff.Count == 0)
            return;

        var message = BuildMessage(contextTag, diff, records);
        _logger.LogError("[SnapshotDiffGuard] {Message}", message);
        throw new SnapshotDiffException(message);
    }

    private static string BuildMessage(string contextTag, IReadOnlyList<string> diff, SharedMoveSnapshot records)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Snapshot drift detected at '{contextTag}'.");
        builder.AppendLine($"Recorded {records.Records.Count} record(s); {diff.Count} divergence(s):");

        foreach (var line in diff)
            builder.AppendLine($"  - {line}");

        return builder.ToString();
    }
}

public class SnapshotDiffException : Exception
{
    public SnapshotDiffException(string message) : base(message)
    {
    }
}

public static class SnapshotDiffCalculator
{
    public static IReadOnlyList<string> Compute(GameStateSnapshot expected, GameStateSnapshot actual)
    {
        var diff = new List<string>();

        ComparePlayers(expected.Players, actual.Players, diff);
        CompareBoards(expected.Boards, actual.Boards, diff);

        return diff;
    }

    private static void ComparePlayers(
        Dictionary<Guid, PlayerStateSnapshot> expected,
        Dictionary<Guid, PlayerStateSnapshot> actual,
        List<string> diff)
    {
        foreach (var (id, expectedPlayer) in expected)
        {
            if (actual.TryGetValue(id, out var actualPlayer) == false)
            {
                diff.Add($"Player {id}: missing in actual state");
                continue;
            }

            ComparePlayer(id, expectedPlayer, actualPlayer, diff);
        }

        foreach (var (id, _) in actual)
        {
            if (expected.ContainsKey(id) == false)
                diff.Add($"Player {id}: unexpected extra in actual state");
        }
    }

    private static void ComparePlayer(
        Guid id,
        PlayerStateSnapshot expected,
        PlayerStateSnapshot actual,
        List<string> diff)
    {
        if (expected.ManaCurrent != actual.ManaCurrent || expected.ManaMax != actual.ManaMax)
        {
            diff.Add($"Player {id} Mana: expected {expected.ManaCurrent}/{expected.ManaMax}, " +
                     $"got {actual.ManaCurrent}/{actual.ManaMax}");
        }

        if (expected.HealthCurrent != actual.HealthCurrent || expected.HealthMax != actual.HealthMax)
        {
            diff.Add($"Player {id} Health: expected {expected.HealthCurrent}/{expected.HealthMax}, " +
                     $"got {actual.HealthCurrent}/{actual.HealthMax}");
        }

        if (expected.MovesLeft != actual.MovesLeft ||
            expected.MovesMax != actual.MovesMax ||
            expected.MovesIsAvailable != actual.MovesIsAvailable)
        {
            diff.Add(
                $"Player {id} Moves: expected {expected.MovesLeft}/{expected.MovesMax} available={expected.MovesIsAvailable}, " +
                $"got {actual.MovesLeft}/{actual.MovesMax} available={actual.MovesIsAvailable}");
        }

        CompareModifiers(id, expected.Modifiers, actual.Modifiers, diff);
        CompareHands(id, expected.Hand, actual.Hand, diff);
        CompareStash(id, expected.Stash, actual.Stash, diff);
    }

    private static void CompareStash(
        Guid playerId,
        List<CardType> expected,
        List<CardType> actual,
        List<string> diff)
    {
        if (expected.Count != actual.Count)
        {
            diff.Add($"Player {playerId} Stash count: expected {expected.Count}, got {actual.Count}");
            return;
        }

        for (var i = 0; i < expected.Count; i++)
        {
            if (expected[i] != actual[i])
                diff.Add($"Player {playerId} Stash[{i}]: expected {expected[i]}, got {actual[i]}");
        }
    }

    private static void CompareModifiers(
        Guid playerId,
        Dictionary<PlayerModifier, float> expected,
        Dictionary<PlayerModifier, float> actual,
        List<string> diff)
    {
        foreach (var (modifier, expectedValue) in expected)
        {
            var actualValue = actual.TryGetValue(modifier, out var value) == true ? value : 0f;

            if (Math.Abs(expectedValue - actualValue) > 0.0001f)
                diff.Add($"Player {playerId} Modifier {modifier}: expected {expectedValue}, got {actualValue}");
        }

        foreach (var (modifier, actualValue) in actual)
        {
            if (expected.ContainsKey(modifier) == false && Math.Abs(actualValue) > 0.0001f)
                diff.Add($"Player {playerId} Modifier {modifier}: expected 0, got {actualValue}");
        }
    }

    private static void CompareHands(
        Guid playerId,
        Dictionary<Guid, CardType> expected,
        Dictionary<Guid, CardType> actual,
        List<string> diff)
    {
        foreach (var (cardId, expectedType) in expected)
        {
            if (actual.TryGetValue(cardId, out var actualType) == false)
            {
                diff.Add($"Player {playerId} Hand: missing card {cardId} ({expectedType})");
                continue;
            }

            if (expectedType != actualType)
                diff.Add($"Player {playerId} Hand {cardId}: expected {expectedType}, got {actualType}");
        }

        foreach (var (cardId, actualType) in actual)
        {
            if (expected.ContainsKey(cardId) == false)
                diff.Add($"Player {playerId} Hand: unexpected card {cardId} ({actualType})");
        }
    }

    private static void CompareBoards(
        Dictionary<Guid, BoardStateSnapshot> expected,
        Dictionary<Guid, BoardStateSnapshot> actual,
        List<string> diff)
    {
        foreach (var (ownerId, expectedBoard) in expected)
        {
            if (actual.TryGetValue(ownerId, out var actualBoard) == false)
            {
                diff.Add($"Board {ownerId}: missing in actual state");
                continue;
            }

            CompareBoard(ownerId, expectedBoard, actualBoard, diff);
        }
    }

    private static void CompareBoard(
        Guid ownerId,
        BoardStateSnapshot expected,
        BoardStateSnapshot actual,
        List<string> diff)
    {
        foreach (var (position, expectedCell) in expected.Cells)
        {
            if (actual.Cells.TryGetValue(position, out var actualCell) == false)
            {
                diff.Add($"Board {ownerId} cell {position}: missing in actual state");
                continue;
            }

            CompareCell(ownerId, position, expectedCell, actualCell, diff);
        }

        foreach (var (position, _) in actual.Cells)
        {
            if (expected.Cells.ContainsKey(position) == false)
                diff.Add($"Board {ownerId} cell {position}: unexpected extra in actual state");
        }
    }

    private static void CompareCell(
        Guid ownerId,
        Position position,
        CellStateSnapshot expected,
        CellStateSnapshot actual,
        List<string> diff)
    {
        if (expected.Status != actual.Status)
        {
            diff.Add($"Board {ownerId} cell {position} Status: expected {expected.Status}, got {actual.Status}");
            return;
        }

        if (expected.Status == CellStatus.Taken && expected.IsFlagged != actual.IsFlagged)
        {
            diff.Add(
                $"Board {ownerId} cell {position} IsFlagged: expected {expected.IsFlagged}, got {actual.IsFlagged}");
        }

        if (expected.Status == CellStatus.Free && expected.MinesAround != actual.MinesAround)
        {
            diff.Add(
                $"Board {ownerId} cell {position} MinesAround: expected {expected.MinesAround}, got {actual.MinesAround}");
        }

        CompareEffects(ownerId, position, expected.Effects, actual.Effects, diff);
    }

    private static void CompareEffects(
        Guid ownerId,
        Position position,
        Dictionary<Guid, CellEffectType> expected,
        Dictionary<Guid, CellEffectType> actual,
        List<string> diff)
    {
        foreach (var (effectId, expectedType) in expected)
        {
            if (actual.TryGetValue(effectId, out var actualType) == false)
            {
                diff.Add($"Board {ownerId} cell {position} Effect {effectId} ({expectedType}): missing in actual");
                continue;
            }

            if (expectedType != actualType)
                diff.Add(
                    $"Board {ownerId} cell {position} Effect {effectId}: expected {expectedType}, got {actualType}");
        }

        foreach (var (effectId, actualType) in actual)
        {
            if (expected.ContainsKey(effectId) == false)
                diff.Add(
                    $"Board {ownerId} cell {position} Effect {effectId} ({actualType}): unexpected extra in actual");
        }
    }
}