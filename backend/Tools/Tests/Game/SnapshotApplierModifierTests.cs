using FluentAssertions;
using Game.GamePlay.Snapshots;
using Shared;
using Xunit;

namespace Tests.Game;

public class SnapshotApplierModifierTests
{
    [Fact]
    public void Apply_AddsNewOverview()
    {
        var pre = new GameStateSnapshot();
        var playerId = Guid.NewGuid();
        pre.Players[playerId] = new PlayerStateSnapshot { Modifiers = new List<DurationalModifierOverview>() };

        var sourceId = Guid.NewGuid();
        var records = new SharedMoveSnapshot
        {
            Records = new List<IMoveSnapshotRecord>
            {
                new PlayerSnapshotRecord.ModifierUpdate
                {
                    PlayerId = playerId,
                    Overview = new DurationalModifierOverview { SourceId = sourceId, Type = PlayerModifier.AdditionalMoves, Value = 2f, Key = "test", TurnsToEnd = 3 }
                }
            }
        };

        var post = SnapshotApplier.Apply(pre, records);
        post.Players[playerId].Modifiers.Should().HaveCount(1);
        post.Players[playerId].Modifiers[0].Value.Should().Be(2f);
    }

    [Fact]
    public void Apply_UpdatesExistingOverview()
    {
        var sourceId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var pre = new GameStateSnapshot();
        pre.Players[playerId] = new PlayerStateSnapshot
        {
            Modifiers = new List<DurationalModifierOverview>
            {
                new DurationalModifierOverview { SourceId = sourceId, Type = PlayerModifier.AdditionalMoves, Value = 2f, Key = "test", TurnsToEnd = 3 }
            }
        };

        var records = new SharedMoveSnapshot
        {
            Records = new List<IMoveSnapshotRecord>
            {
                new PlayerSnapshotRecord.ModifierUpdate
                {
                    PlayerId = playerId,
                    Overview = new DurationalModifierOverview { SourceId = sourceId, Type = PlayerModifier.AdditionalMoves, Value = 2f, Key = "test", TurnsToEnd = 2 }
                }
            }
        };

        var post = SnapshotApplier.Apply(pre, records);
        post.Players[playerId].Modifiers.Should().HaveCount(1);
        post.Players[playerId].Modifiers[0].TurnsToEnd.Should().Be(2);
    }

    [Fact]
    public void Apply_RemovesExpiredOverview()
    {
        var sourceId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var pre = new GameStateSnapshot();
        pre.Players[playerId] = new PlayerStateSnapshot
        {
            Modifiers = new List<DurationalModifierOverview>
            {
                new DurationalModifierOverview { SourceId = sourceId, Type = PlayerModifier.AdditionalMoves, Value = 2f, Key = "test", TurnsToEnd = 1 }
            }
        };

        var records = new SharedMoveSnapshot
        {
            Records = new List<IMoveSnapshotRecord>
            {
                new PlayerSnapshotRecord.ModifierUpdate
                {
                    PlayerId = playerId,
                    Overview = new DurationalModifierOverview { SourceId = sourceId, Type = PlayerModifier.AdditionalMoves, Value = 2f, Key = "test", TurnsToEnd = 0 }
                }
            }
        };

        var post = SnapshotApplier.Apply(pre, records);
        post.Players[playerId].Modifiers.Should().BeEmpty();
    }
}
