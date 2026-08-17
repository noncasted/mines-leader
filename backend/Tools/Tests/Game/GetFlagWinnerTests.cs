using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests RoundPlayers.GetFlagWinner() — determines if a player has flagged all mines
/// on their opponent's board.
/// </summary>
public class GetFlagWinnerTests
{
    private static (IGameContext Context, Guid Player1Id, Guid Player2Id) CreateContext(
        IBoard board1,
        IBoard board2)
    {
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        var user1 = Substitute.For<IUser>();
        user1.Id.Returns(player1Id);

        var user2 = Substitute.For<IUser>();
        user2.Id.Returns(player2Id);

        var player1 = Substitute.For<IPlayer>();
        player1.User.Returns(user1);
        player1.Board.Returns(board1);

        var player2 = Substitute.For<IPlayer>();
        player2.User.Returns(user2);
        player2.Board.Returns(board2);

        var boards = new Dictionary<IPlayer, IBoard>
        {
            { player1, board1 },
            { player2, board2 }
        };

        var context = Substitute.For<IGameContext>();
        context.Boards.Returns(boards);

        return (context, player1Id, player2Id);
    }

    [Fact]
    public void AllMinesFlagged_ReturnsWinnerId()
    {
        // Player 1's board has all mines flagged — player 1 wins
        var (board1, _) = BoardParser.Parse("""
                                            t t t t t
                                            t f t t t
                                            t t f t t
                                            t t t f t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, player1Id, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(player1Id);
    }

    [Fact]
    public void SomeMinesUnflagged_ReturnsEmpty()
    {
        // Both boards have unflagged mines
        var (board1, _) = BoardParser.Parse("""
                                            t t t t t
                                            t f t t t
                                            t t m t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, _, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(Guid.Empty);
    }

    [Fact]
    public void NoMinesOnBoard_FirstPlayerWins()
    {
        // Board with no mines — allMinesFlagged stays true (vacuous truth)
        var (board1, _) = BoardParser.Parse("""
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, player1Id, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(player1Id);
    }

    [Fact]
    public void BothBoardsAllFlagged_FirstPlayerWins()
    {
        // Both boards have all mines flagged — first iterated player wins
        var (board1, _) = BoardParser.Parse("""
                                            f t t
                                            t t t
                                            t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            t t t
                                            t f t
                                            t t t
                                            """);

        var (context, player1Id, player2Id) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        // Dictionary iteration order is insertion order — player1 is checked first
        winner.Should().Be(player1Id);
    }

    [Fact]
    public void EmptyBoard_NoCells_SkipsPlayer()
    {
        // Board with 0 cells is skipped by the count check
        var emptyBoard = Substitute.For<IBoard>();
        emptyBoard.Cells.Returns(new Dictionary<Position, ICell>());

        var (board2, _) = BoardParser.Parse("""
                                            m t t
                                            t t t
                                            t t t
                                            """);

        var player1 = Substitute.For<IPlayer>();
        var user1 = Substitute.For<IUser>();
        user1.Id.Returns(Guid.NewGuid());
        player1.User.Returns(user1);

        var player2 = Substitute.For<IPlayer>();
        var user2 = Substitute.For<IUser>();
        user2.Id.Returns(Guid.NewGuid());
        player2.User.Returns(user2);

        var boards = new Dictionary<IPlayer, IBoard>
        {
            { player1, emptyBoard },
            { player2, board2 }
        };

        var context = Substitute.For<IGameContext>();
        context.Boards.Returns(boards);

        var roundPlayers = new RoundPlayers(context);
        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(Guid.Empty, "empty board is skipped, second board has unflagged mines");
    }

    [Fact]
    public void UnflaggedMine_OpponentNoMines_OpponentWinsByVacuousTruth()
    {
        // Board1 has unflagged mine at (0,0) — player1 does NOT win
        // Board2 has no mines — vacuous truth: "all mines flagged" is true, player2 wins
        var (board1, _) = BoardParser.Parse("""
                                            m t t t t
                                            t g t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, _, player2Id) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        // Player1 has unflagged mine => not a winner
        // Player2's board has zero mines => vacuous truth => player2 wins
        winner.Should().Be(player2Id);
    }

    [Fact]
    public void FreeCells_AreSkipped()
    {
        // GetFlagWinner skips Free cells — they can't have mines
        var (board1, _) = BoardParser.Parse("""
                                            f t t t t
                                            t _ t t t
                                            t t _ t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, player1Id, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(player1Id, "all mines are flagged, Free cells are skipped");
    }

    [Fact]
    public void SingleMine_Unflagged_NoWinner()
    {
        var (board1, _) = BoardParser.Parse("""
                                            t t t
                                            t m t
                                            t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t
                                            t t t
                                            t t t
                                            """);

        var (context, _, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(Guid.Empty);
    }

    [Fact]
    public void AllMinesFlagged_ExtraFlagOnSafeCell_NoWinner()
    {
        // All mines are flagged, but there's an extra flag on a safe cell
        // This should NOT produce a winner (counter would show negative)
        var (board1, _) = BoardParser.Parse("""
                                            f f f f f
                                            t t t t g
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, _, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(Guid.Empty, "extra flag on safe cell prevents win");
    }

    [Fact]
    public void ManyMines_AllFlagged_Winner()
    {
        var (board1, _) = BoardParser.Parse("""
                                            f f f f f
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, player1Id, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(player1Id);
    }

    [Fact]
    public void ManyMines_OneMissing_NoWinner()
    {
        var (board1, _) = BoardParser.Parse("""
                                            f f f f m
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, _, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(Guid.Empty);
    }

    [Fact]
    public void DetonatedMine_NoWinner()
    {
        // Оставшаяся мина не отмечена флагом, а подорвана — с поля она исчезла,
        // но победу по флагам это давать не должно.
        var (board1, _) = BoardParser.Parse("""
                                            f f f f _
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        board1.RegisterDetonatedMine();

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, _, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(Guid.Empty);
    }

    [Fact]
    public void AllMinesFlagged_NoDetonations_ReturnsWinnerId()
    {
        // Та же доска, но мина отмечена флагом, а не подорвана — победа засчитывается.
        var (board1, _) = BoardParser.Parse("""
                                            f f f f f
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (board2, _) = BoardParser.Parse("""
                                            m t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            t t t t t
                                            """);

        var (context, player1Id, _) = CreateContext(board1, board2);
        var roundPlayers = new RoundPlayers(context);

        var winner = roundPlayers.GetFlagWinner();

        winner.Should().Be(player1Id);
    }
}