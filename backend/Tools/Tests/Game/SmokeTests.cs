using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Smoke: adds SmokeEffect to all cells in a rhombus pattern (both Taken and Free).
/// Schedules a SmokeDisposeAction via IRoundActionService for cleanup after Duration rounds.
/// SmokeDisposeAction removes the effect from all affected cells when executed.
/// </summary>
public class SmokeTests : PlayerCardTestsBase
{
    private CardUseResult Use(IBoard board, CardUsePayload.Smoke payload, IRoundActionService roundActionService)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new Smoke(MockConfigs(), roundActionService, gameContext).Use(invoker, payload);
    }

    private (CardUseResult, MoveSnapshot) UseCapture(
        IBoard board,
        CardUsePayload.Smoke payload,
        IRoundActionService roundActionService)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new Smoke(MockConfigs(), roundActionService, gameContext).UseCapture(invoker, payload);
    }

    [Fact]
    public void Use_AddsSmokeEffectToCells()
    {
        // Rhombus(3) = 3x3 cross pattern = 5 cells centered on target (2,2)
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var roundActionService = Substitute.For<IRoundActionService>();

        var result = Use(board, new CardUsePayload.Smoke { Position = target }, roundActionService);

        result.Result.HasError.Should().BeFalse();

        // Rhombus(3) cross: (2,1), (1,2), (2,2), (3,2), (2,3)
        var cellsWithSmoke = board.Cells.Values
                                  .Where(c => c.Effects.Any(e => e.Type == CellEffectType.Smoke))
                                  .ToList();

        cellsWithSmoke.Should().HaveCount(5, "Rhombus(3) is a cross pattern with 5 cells");

        var smokePositions = cellsWithSmoke.Select(c => c.Position).ToHashSet();
        smokePositions.Should().Contain(new Position(2, 1));
        smokePositions.Should().Contain(new Position(1, 2));
        smokePositions.Should().Contain(new Position(2, 2));
        smokePositions.Should().Contain(new Position(3, 2));
        smokePositions.Should().Contain(new Position(2, 3));

        // All effects should be Smoke type
        var effects = cellsWithSmoke.SelectMany(c => c.Effects).ToList();
        effects.Should().AllSatisfy(e => e.Type.Should().Be(CellEffectType.Smoke));
    }

    [Fact]
    public void Use_SchedulesDispose()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var roundActionService = Substitute.For<IRoundActionService>();

        Use(board, new CardUsePayload.Smoke { Position = target }, roundActionService);

        roundActionService.Received(1)
                          .Schedule(Arg.Any<SmokeDisposeAction>(),
                              CardConfigs.Smoke.Duration);
    }

    [Fact]
    public void Use_AffectsBothTakenAndFreeCells()
    {
        // Mix of Taken and Free cells
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t _ _ _ t
                                                t _ x _ t
                                                t _ _ _ t
                                                t t t t t
                                                """);

        var roundActionService = Substitute.For<IRoundActionService>();

        Use(board, new CardUsePayload.Smoke { Position = target }, roundActionService);

        // Both Free and Taken cells in range should have smoke
        var cellsWithSmoke = board.Cells.Values
                                  .Where(c => c.Effects.Any(e => e.Type == CellEffectType.Smoke))
                                  .ToList();

        cellsWithSmoke.Should().HaveCount(5, "Rhombus(3) cross pattern = 5 cells");

        var takenWithSmoke = cellsWithSmoke.Where(c => c.Status == CellStatus.Taken).ToList();
        var freeWithSmoke = cellsWithSmoke.Where(c => c.Status == CellStatus.Free).ToList();

        takenWithSmoke.Should().NotBeEmpty("at least one Taken cell should have smoke");
        freeWithSmoke.Should().NotBeEmpty("at least one Free cell should have smoke");

        // Verify specific positions: center (2,2) is Taken (x), neighbors (1,2),(3,2) are Free
        takenWithSmoke.Select(c => c.Position).Should().Contain(new Position(2, 2));
        freeWithSmoke.Select(c => c.Position).Should().Contain(new Position(1, 2));
    }

    [Fact]
    public void Use_EmptyBoard_Fails()
    {
        var emptyBoard = new TestBoardBuilder(0).Build();
        var roundActionService = Substitute.For<IRoundActionService>();

        var result = Use(emptyBoard, new CardUsePayload.Smoke { Position = new Position(0, 0) }, roundActionService);

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var ownerId = board.OwnerId;
        var roundActionService = Substitute.For<IRoundActionService>();

        var (_, moveSnapshot) = UseCapture(board, new CardUsePayload.Smoke { Position = target }, roundActionService);

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.Smoke>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }

    [Fact]
    public void DisposeAction_RemovesEffects()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var roundActionService = new RoundActionService();

        Use(board, new CardUsePayload.Smoke { Position = target }, roundActionService);

        // Verify effects exist
        board.Cells.Values.Any(c => c.Effects.Any(e => e.Type == CellEffectType.Smoke))
             .Should()
             .BeTrue();

        // Tick Duration times to trigger dispose
        for (var i = 0; i < CardConfigs.Smoke.Duration; i++)
            roundActionService.Tick(new MoveSnapshot());

        // Effects should be removed
        board.Cells.Values.Any(c => c.Effects.Any(e => e.Type == CellEffectType.Smoke))
             .Should()
             .BeFalse();
    }

    [Fact]
    public void Use_AllEffectsShareSameId()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var roundActionService = Substitute.For<IRoundActionService>();

        Use(board, new CardUsePayload.Smoke { Position = target }, roundActionService);

        var effectIds = board.Cells.Values
                             .SelectMany(c => c.Effects)
                             .Where(e => e.Type == CellEffectType.Smoke)
                             .Select(e => e.Id)
                             .Distinct()
                             .ToList();

        effectIds.Should().HaveCount(1, "all smoke effects from one card should share the same ID");
    }
}