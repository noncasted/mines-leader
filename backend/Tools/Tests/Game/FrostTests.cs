using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class FrostTests
{
    [Fact]
    public void Use_AddsFrostEffectToCellsInPattern()
    {
        var board = new TestBoardBuilder(5).Build();
        var roundService = Substitute.For<IRoundActionService>();
        var config = CardConfigs.Frost;

        var result = new Frost(board, config,
            new CardUsePayload.Frost { Position = new Position(2, 2) },
            roundService).Use();

        result.Result.HasError.Should().BeFalse();

        // Center cell should have a Frost effect
        var center = board.Cells[new Position(2, 2)];
        center.Effects.Should().ContainSingle(e => e.Type == CellEffectType.Frost);
    }

    [Fact]
    public void Use_SchedulesDisposeActionWithConfigDuration()
    {
        var board = new TestBoardBuilder(5).Build();
        var roundService = Substitute.For<IRoundActionService>();
        var config = CardConfigs.Frost;

        new Frost(board, config,
            new CardUsePayload.Frost { Position = new Position(2, 2) },
            roundService).Use();

        roundService.Received(1).Schedule(Arg.Any<FrostDisposeAction>(), config.Duration);
    }

    [Fact]
    public void Use_DisposeActionRemovesFrostEffectAfterDuration()
    {
        var board = new TestBoardBuilder(5).Build();
        var roundService = new RoundActionService();
        var config = new CardConfigOptions.Frost { Size = 1, Duration = 1 };

        new Frost(board, config,
            new CardUsePayload.Frost { Position = new Position(2, 2) },
            roundService).Use();

        roundService.Tick();

        var center = board.Cells[new Position(2, 2)];
        center.Effects.Should().NotContain(e => e.Type == CellEffectType.Frost);
    }

    [Fact]
    public void Use_ActionDataIncludesFrozenCellsAndTargetPlayer()
    {
        var ownerId = Guid.NewGuid();
        var board = new TestBoardBuilder(5).WithOwner(ownerId).Build();
        var roundService = Substitute.For<IRoundActionService>();
        var config = new CardConfigOptions.Frost { Size = 1, Duration = 1 };

        var result = new Frost(board, config,
            new CardUsePayload.Frost { Position = new Position(2, 2) },
            roundService).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Frost>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
        actionData.FrozenCells.Should().NotBeEmpty();
    }
}
