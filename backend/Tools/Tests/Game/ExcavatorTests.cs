using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class ExcavatorTests : PlayerCardTestsBase
{
    private static IPlayer MockInvoker(IBoard board)
    {
        var player = MockPlayer();
        player.Board.Returns(board);
        return player;
    }

    [Fact]
    public void Use_FlagsMinesAndRevealsSafeCellsInCross()
    {
        // Cross pattern centered on x — mines get flagged, safe taken cells get revealed
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t m t t
                                                t m x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var invoker = MockInvoker(board);
        var card = new Excavator(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Excavator { Position = target });

        result.Result.HasError.Should().BeFalse();

        BoardParser.AssertBoard(board, """
                                       t t t t t
                                       t t f t t
                                       t f R R t
                                       t t R t t
                                       t t t t t
                                       """);
    }

    [Fact]
    public void Use_AllTakenNoMines_FloodFillsEntireBoard()
    {
        // With no mines anywhere, revealing cross cells flood-fills the whole board
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var invoker = MockInvoker(board);
        var card = new Excavator(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Excavator { Position = target });

        result.Result.HasError.Should().BeFalse();

        // Flood fill expands to entire board when no mines contain it
        BoardParser.AssertBoard(board, """
                                       R R R R R
                                       R R R R R
                                       R R R R R
                                       R R R R R
                                       R R R R R
                                       """);
    }

    [Fact]
    public void Use_NoTakenCellsInPattern_Fails()
    {
        // All cells around target are free — no taken cells to select
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           _ _ _ _ _
                                           """);

        var invoker = MockInvoker(board);
        var card = new Excavator(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Excavator { Position = new Position(2, 2) });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ActionDataIncludesBoardOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var builtBoard = new TestBoardBuilder(3).WithOwner(ownerId).Build();

        var invoker = MockInvoker(builtBoard);
        var card = new Excavator(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Excavator { Position = new Position(1, 1) });

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Excavator>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}