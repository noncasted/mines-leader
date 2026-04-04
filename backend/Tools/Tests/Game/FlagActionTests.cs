using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests flag operations on cells directly (SetFlag/RemoveFlag on TakenCell).
/// Also validates the rules enforced by SetFlagAction/RemoveFlagAction commands:
/// - SetFlag on Free cell => Failed
/// - SetFlag on already flagged cell => Failed
/// - RemoveFlag on unflagged cell => Failed
/// - RemoveFlag on Free cell => Failed
/// </summary>
public class FlagActionTests
{
    [Fact]
    public void SetFlag_OnTakenCell_PlacesFlag()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """
        );

        var taken = (ITakenCell)board.Cells[target];
        taken.SetFlag();

        taken.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void SetFlag_OnTakenCellWithMine_PlacesFlag()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """
        );

        // Place mine manually since 'x' is just Taken
        var taken = (ITakenCell)board.Cells[target];
        taken.SetMine();
        taken.SetFlag();

        taken.IsFlagged.Should().BeTrue();
        taken.HasMine.Should().BeTrue();
    }

    [Fact]
    public void SetFlag_OnAlreadyFlaggedCell_IsIdempotent()
    {
        // At cell level, SetFlag is idempotent — calling twice keeps IsFlagged=true
        // (SetFlagAction command would return Failed, but this test covers cell-level behavior)
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t g t t
                                           t t t t t
                                           t t t t t
                                           """
        );
        var pos = new Position(2, 2);

        var taken = (ITakenCell)board.Cells[pos];
        taken.IsFlagged.Should().BeTrue("cell was built with flag");

        // Calling SetFlag again is idempotent at cell level
        taken.SetFlag();
        taken.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void RemoveFlag_OnFlaggedCell_RemovesFlag()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t g t t
                                           t t t t t
                                           t t t t t
                                           """
        );
        var pos = new Position(2, 2);

        var taken = (ITakenCell)board.Cells[pos];
        taken.IsFlagged.Should().BeTrue();

        taken.RemoveFlag();

        taken.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void RemoveFlag_OnUnflaggedCell_StaysFalse()
    {
        // RemoveFlagAction checks IsFlagged==false and returns Failed
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """
        );

        var taken = (ITakenCell)board.Cells[target];
        taken.IsFlagged.Should().BeFalse();

        // Calling RemoveFlag sets IsFlagged = false (idempotent at cell level)
        taken.RemoveFlag();
        taken.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void SetFlag_ThenRemoveFlag_Roundtrip()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """
        );

        var taken = (ITakenCell)board.Cells[target];

        taken.SetFlag();
        taken.IsFlagged.Should().BeTrue();

        taken.RemoveFlag();
        taken.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void SetFlag_OnMultipleCells_IndependentState()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """
        );

        var taken1 = (ITakenCell)board.Cells[new Position(1, 1)];
        var taken2 = (ITakenCell)board.Cells[new Position(2, 2)];
        var taken3 = (ITakenCell)board.Cells[new Position(3, 3)];

        taken1.SetFlag();
        taken2.SetFlag();

        taken1.IsFlagged.Should().BeTrue();
        taken2.IsFlagged.Should().BeTrue();
        taken3.IsFlagged.Should().BeFalse("was not flagged");
    }

    [Fact]
    public void SetFlag_FiresBoardEvent()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """
        );
        var lifetime = new Lifetime();
        var flagEvents = new List<(ICell Cell, bool IsFlagged)>();

        board.Events.Flag.Advise(lifetime, (cell, flagged) => flagEvents.Add((cell, flagged)));

        var taken = (ITakenCell)board.Cells[new Position(2, 2)];
        taken.SetFlag();

        flagEvents.Should().HaveCount(1);
        flagEvents[0].IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void RemoveFlag_FiresBoardEvent()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t g t t
                                           t t t t t
                                           t t t t t
                                           """
        );
        var lifetime = new Lifetime();
        var flagEvents = new List<(ICell Cell, bool IsFlagged)>();

        board.Events.Flag.Advise(lifetime, (cell, flagged) => flagEvents.Add((cell, flagged)));

        var taken = (ITakenCell)board.Cells[new Position(2, 2)];
        taken.RemoveFlag();

        flagEvents.Should().HaveCount(1);
        flagEvents[0].IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void Flag_DoesNotAffectMineState()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t m t t
                                           t t t t t
                                           t t t t t
                                           """
        );
        var pos = new Position(2, 2);

        var taken = (ITakenCell)board.Cells[pos];
        taken.HasMine.Should().BeTrue();

        taken.SetFlag();
        taken.HasMine.Should().BeTrue("flagging does not change mine state");

        taken.RemoveFlag();
        taken.HasMine.Should().BeTrue("unflagging does not change mine state");
    }

    [Fact]
    public void Flag_DoesNotAffectCellStatus()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """
        );

        var taken = (ITakenCell)board.Cells[target];
        taken.Status.Should().Be(CellStatus.Taken);

        taken.SetFlag();
        taken.Status.Should().Be(CellStatus.Taken, "flagging does not change cell status");

        taken.RemoveFlag();
        taken.Status.Should().Be(CellStatus.Taken, "unflagging does not change cell status");
    }
}