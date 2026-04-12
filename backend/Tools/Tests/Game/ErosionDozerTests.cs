using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// ErosionDozer (Size=3 from config): finds Taken cells bordering Free via GetClosedShape,
/// opens closest Size cells, then Reveal flood-fills from each.
/// Requires at least one Free cell adjacent to target for GetClosedShape to find a border.
/// </summary>
public class ErosionDozerTests : PlayerCardTestsBase
{
    private static IPlayer MockInvoker(IBoard board)
    {
        var player = MockPlayer();
        player.Board.Returns(board);
        return player;
    }

    [Fact]
    public void Use_ErodesFromFreeBorder()
    {
        // Free cell at (4,2), target at adjacent Taken (4,4) — dozer finds border shape
        var (board, target) = BoardParser.Parse("""
                                                t t t t t m t t t t
                                                t t m t t t t t t t
                                                t t t t _ t t m t t
                                                t t t t t t t t t t
                                                t m t t x t t t t t
                                                t t t t t t t t m t
                                                t t t t t t m t t t
                                                t t m t t t t t t t
                                                t t t t t t t t t t
                                                t t t t m t t t m t
                                                """);

        var invoker = MockInvoker(board);
        var card = new ErosionDozer(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.ErosionDozer { Position = target });

        result.Result.HasError.Should().BeFalse();

        // Dozer opens 3 closest Taken cells bordering the Free region, then reveal expands
        BoardParser.AssertBoard(board, """
                                       t t t t t m t t t t
                                       t t m R R R R t t t
                                       t t R R R R R m t t
                                       t t R R R R R R t t
                                       t m R R R R R R t t
                                       t t R R R R R R m t
                                       t t R R R R m t t t
                                       t t m R R R t t t t
                                       t t t R R R t t t t
                                       t t t t m t t t m t
                                       """);
    }

    [Fact]
    public void Use_NoFreeCells_Fails()
    {
        // All Taken — GetClosedShape never finds a border
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var invoker = MockInvoker(board);
        var card = new ErosionDozer(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.ErosionDozer { Position = target });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_MinesContainReveal()
    {
        // Tight mine ring limits how far reveal can expand after eroding
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t
                                                t m m m m m t
                                                t m t t _ m t
                                                t m t x t m t
                                                t m t t t m t
                                                t m m m m m t
                                                t t t t t t t
                                                """);

        var invoker = MockInvoker(board);
        var card = new ErosionDozer(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.ErosionDozer { Position = target });

        result.Result.HasError.Should().BeFalse();

        // Reveal stays inside mine ring
        BoardParser.AssertBoard(board, """
                                       t t t t t t t
                                       t m m m m m t
                                       t m R R R m t
                                       t m R R R m t
                                       t m R R R m t
                                       t m m m m m t
                                       t t t t t t t
                                       """);
    }

    [Fact]
    public void Use_TargetIsFree_Fails()
    {
        // GetClosedShape starts from Free cell → returns true immediately, adds nothing
        var (board, _) = BoardParser.Parse("""
                                           t t t m t
                                           t t t t t
                                           t t _ t t
                                           t t t t t
                                           t m t t t
                                           """);

        var invoker = MockInvoker(board);
        var card = new ErosionDozer(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.ErosionDozer { Position = new Position(2, 2) });

        result.Result.HasError.Should().BeTrue();
    }
}
