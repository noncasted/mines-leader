using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class FrostTests : PlayerCardTestsBase
{
    private CardUseResult Use(IBoard board, CardUsePayload.Frost payload, IRoundActionService roundActionService,
        ICardConfigs? configs = null)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new Frost(configs ?? MockConfigs(), roundActionService, gameContext).Use(invoker, payload);
    }

    [Fact]
    public void Use_AddsFrostEffectToCellsInPattern()
    {
        var board = new TestBoardBuilder(5).Build();
        var roundService = Substitute.For<IRoundActionService>();

        var result = Use(board, new CardUsePayload.Frost { Position = new Position(2, 2) }, roundService);

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

        Use(board, new CardUsePayload.Frost { Position = new Position(2, 2) }, roundService);

        roundService.Received(1).Schedule(Arg.Any<FrostDisposeAction>(), config.Duration);
    }

    [Fact]
    public void Use_DisposeActionRemovesFrostEffectAfterDuration()
    {
        var board = new TestBoardBuilder(5).Build();
        var roundService = new RoundActionService();
        var allConfigs = CardConfigs.All;
        allConfigs.Frost_Normal = new CardConfigOptions.Frost { Size = 1, Duration = 1 };
        var configs = Substitute.For<ICardConfigs>();
        configs.Value.Returns(allConfigs);

        Use(board, new CardUsePayload.Frost { Position = new Position(2, 2) }, roundService, configs);

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
        var allConfigs = CardConfigs.All;
        allConfigs.Frost_Normal = new CardConfigOptions.Frost { Size = 1, Duration = 1 };
        var configs = Substitute.For<ICardConfigs>();
        configs.Value.Returns(allConfigs);

        var result = Use(board, new CardUsePayload.Frost { Position = new Position(2, 2) }, roundService, configs);

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Frost>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
        actionData.FrozenCells.Should().NotBeEmpty();
    }
}
