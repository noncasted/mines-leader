using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// OpponentBomb: targets a single cell on opponent's board.
/// - Taken + mine: explode (stays Taken), deal 1 damage, then Reveal from that position
/// - Taken + no mine: convert to Free, then Reveal flood-fills safe area
/// - Free: fail
/// </summary>
public class OpponentBombTests
{
    private static IPlayer MockOpponent()
    {
        var player = Substitute.For<IPlayer>();
        player.Health.Returns(Substitute.For<IHealth>());
        return player;
    }

    [Fact]
    public void Use_NoMine_RevealsLargeSafeArea()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t m t t t t
                                                t t m t t t t t t t
                                                t t t t t t t m t t
                                                t t t t t t t t t t
                                                t m t t x t t t t t
                                                t t t t t t t t m t
                                                t t t t t t m t t t
                                                t t m t t t t t t t
                                                t t t t t t t t t t
                                                t t t t m t t t m t
                                                """);

        var opponent = MockOpponent();
        new OpponentBomb(opponent, board, new CardUsePayload.OpponentBomb { Position = target }).Use();

        // No mine at target — opens to Free, then flood-fill reveals connected safe area
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

        opponent.Health.DidNotReceive().TakeDamage(Arg.Any<int>());
    }

    [Fact]
    public void Use_HitsMine_ExplodesAndDealsDamage()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t m t t
                                                t t t t t
                                                t t t t t
                                                """);

        // Target the mine cell
        var opponent = MockOpponent();

        new OpponentBomb(opponent, board,
            new CardUsePayload.OpponentBomb { Position = new Position(2, 2) }).Use();

        opponent.Health.Received(1).TakeDamage(1);

        // Mine cell stays Taken (explode doesn't convert to Free)
        // But reveal from that position opens neighbors without mines
        board.Cells[new Position(2, 2)].Status.Should().Be(CellStatus.Taken);
    }

    [Fact]
    public void Use_TightMineRing_RevealContained()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t
                                                t m m m m m t
                                                t m t t t m t
                                                t m t x t m t
                                                t m t t t m t
                                                t m m m m m t
                                                t t t t t t t
                                                """);

        var opponent = MockOpponent();
        new OpponentBomb(opponent, board, new CardUsePayload.OpponentBomb { Position = target }).Use();

        // Reveal cannot escape mine ring
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
    public void Use_FreeCell_Fails()
    {
        var (board, _) = BoardParser.Parse("""
                                           _ _ _
                                           _ _ _
                                           _ _ _
                                           """);

        var result = new OpponentBomb(MockOpponent(), board,
            new CardUsePayload.OpponentBomb { Position = new Position(1, 1) }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_OutOfBounds_Fails()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);

        var result = new OpponentBomb(MockOpponent(), board,
            new CardUsePayload.OpponentBomb { Position = new Position(99, 99) }).Use();

        result.Result.HasError.Should().BeTrue();
    }
}