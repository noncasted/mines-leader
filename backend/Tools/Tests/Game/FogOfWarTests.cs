using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// FogOfWar: adds FogEffect to Free cells only in a rhombus pattern.
/// Taken cells are NOT affected (uses SelectFree).
/// Schedules FogDisposeAction via IRoundActionService for cleanup after Duration rounds.
/// </summary>
public class FogOfWarTests : PlayerCardTestsBase
{
    private CardUseResult Use(IBoard board, CardUsePayload.FogOfWar payload, IRoundActionService roundActionService)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new FogOfWar(MockConfigs(), roundActionService, gameContext).Use(invoker, payload);
    }

    private (CardUseResult, MoveSnapshot) UseCapture(
        IBoard board,
        CardUsePayload.FogOfWar payload,
        IRoundActionService roundActionService)
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var gameContext = MockGameContext(invoker, opponent);
        return new FogOfWar(MockConfigs(), roundActionService, gameContext).UseCapture(invoker, payload);
    }

    [Fact]
    public void Use_AddsFogToFreeCells()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t _ _ _ t
                                                t _ x _ t
                                                t _ _ _ t
                                                t t t t t
                                                """);

        var roundActionService = Substitute.For<IRoundActionService>();

        // Target is Taken (x), but we target center of Free area
        var result = Use(board, new CardUsePayload.FogOfWar { Position = new Position(2, 2) }, roundActionService);

        result.Result.HasError.Should().BeFalse();

        // Only Free cells should have Fog effect
        var cellsWithFog = board.Cells.Values
                                .Where(c => c.Effects.Any(e => e.Type == CellEffectType.Fog))
                                .ToList();

        cellsWithFog.Should().NotBeEmpty();

        cellsWithFog.Should()
                    .AllSatisfy(c => c.Status.Should().Be(CellStatus.Free, "Fog should only be added to Free cells"));
    }

    [Fact]
    public void Use_TakenCellsNotAffected()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t _ t _ t
                                           t t _ t t
                                           t _ t _ t
                                           t t t t t
                                           """);

        var roundActionService = Substitute.For<IRoundActionService>();

        Use(board, new CardUsePayload.FogOfWar { Position = new Position(2, 2) }, roundActionService);

        // Taken cells should NOT have Fog
        var takenWithFog = board.Cells.Values
                                .Where(c => c.Status == CellStatus.Taken &&
                                            c.Effects.Any(e => e.Type == CellEffectType.Fog))
                                .ToList();

        takenWithFog.Should().BeEmpty("Taken cells should not receive Fog effect");
    }

    [Fact]
    public void Use_NoFreeCellsInPattern_Fails()
    {
        // All Taken — SelectFree returns empty
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var roundActionService = Substitute.For<IRoundActionService>();

        var result = Use(board, new CardUsePayload.FogOfWar { Position = target }, roundActionService);

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_SchedulesDispose()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t _ _ t
                                           t _ t t t
                                           t t t t t
                                           """);

        var roundActionService = Substitute.For<IRoundActionService>();

        Use(board, new CardUsePayload.FogOfWar { Position = new Position(2, 2) }, roundActionService);

        roundActionService.Received(1)
                          .Schedule(Arg.Any<FogDisposeAction>(),
                              CardConfigs.FogOfWar.Duration);
    }

    [Fact]
    public void Use_EffectHasCorrectType()
    {
        // FogOfWar uses Rhombus(4) = 4x4 diamond with 12 cells, but only Free cells get fog.
        // Free cells in the pattern centered at (2,2): (1,1), (2,2), (3,3)
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t _ t t t
                                           t t _ t t
                                           t _ t _ t
                                           t t t t t
                                           """);

        var roundActionService = Substitute.For<IRoundActionService>();

        Use(board, new CardUsePayload.FogOfWar { Position = new Position(2, 2) }, roundActionService);

        var cellsWithFog = board.Cells.Values
                                .Where(c => c.Effects.Any(e => e.Type == CellEffectType.Fog))
                                .ToList();

        // Only Free cells within the Rhombus(4) pattern get fog
        cellsWithFog.Should().NotBeEmpty();
        cellsWithFog.Should().AllSatisfy(c => c.Status.Should().Be(CellStatus.Free, "only Free cells receive fog"));

        // Verify the positions match expected Free cells in the rhombus
        var fogPositions = cellsWithFog.Select(c => c.Position).ToHashSet();
        fogPositions.Should().Contain(new Position(1, 1));
        fogPositions.Should().Contain(new Position(2, 2));
    }

    [Fact]
    public void Use_EmptyBoard_Fails()
    {
        var emptyBoard = new TestBoardBuilder(0).Build();
        var roundActionService = Substitute.For<IRoundActionService>();

        var result = Use(emptyBoard, new CardUsePayload.FogOfWar { Position = new Position(0, 0) }, roundActionService);

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t _ _ t
                                           t t t t t
                                           t t t t t
                                           """);

        var ownerId = board.OwnerId;
        var roundActionService = Substitute.For<IRoundActionService>();

        var (_, moveSnapshot) = UseCapture(board, new CardUsePayload.FogOfWar { Position = new Position(2, 2) },
            roundActionService);

        var actionData = moveSnapshot.GetLastCardAction<CardActionSnapshot.FogOfWar>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }

    [Fact]
    public void DisposeAction_RemovesFogEffects()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t _ t t t
                                           _ _ _ t t
                                           t _ t t t
                                           t t t t t
                                           """);

        var roundActionService = new RoundActionService();

        Use(board, new CardUsePayload.FogOfWar { Position = new Position(2, 2) }, roundActionService);

        // Verify effects exist
        board.Cells.Values.Any(c => c.Effects.Any(e => e.Type == CellEffectType.Fog))
             .Should()
             .BeTrue();

        // Tick Duration times
        for (var i = 0; i < CardConfigs.FogOfWar.Duration; i++)
            roundActionService.Tick(new MoveSnapshot());

        // Effects should be removed
        board.Cells.Values.Any(c => c.Effects.Any(e => e.Type == CellEffectType.Fog))
             .Should()
             .BeFalse();
    }
}