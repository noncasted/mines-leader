using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Bot card aiming counts only what is visible: closed cells, flags, numbers.
/// The geometry matches the card itself, so the center is checked through the same PatternShapes.
/// </summary>
public class BotCardTargetingTests
{
    [Fact]
    public void Bloodhound_CoversTheLargestUnflaggedCluster()
    {
        // Two closed columns on the left, one on the right: the best diamond covers the whole left cluster.
        var (board, _) = BoardParser.Parse("""
                                           t t _ _ _ _ t
                                           t t _ _ _ _ t
                                           t t _ _ _ _ t
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           """);

        var centre = BotCardTargeting.BestBloodhoundCentre(board, 3);

        var covered = PatternShapes.Rhombus(3).SelectTaken(board, centre);
        covered.Should().HaveCountGreaterThan(3);
        covered.Should().OnlyContain(c => c.Position.x <= 1);
    }

    [Fact]
    public void Bloodhound_IgnoresFlaggedCells()
    {
        // The left cluster is fully under flags, the right one is clean: the diamond goes right.
        var (board, _) = BoardParser.Parse("""
                                           f f _ _ _ t t
                                           f f _ _ _ t t
                                           f f _ _ _ t t
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           """);

        var centre = BotCardTargeting.BestBloodhoundCentre(board, 3);

        var covered = PatternShapes.Rhombus(3).SelectTaken(board, centre);
        covered.Should().NotBeEmpty();
        covered.Should().OnlyContain(c => c.Position.x >= 5);
    }

    [Fact]
    public void Bloodhound_NothingToCover_ReturnsNone()
    {
        var (board, _) = BoardParser.Parse("""
                                           _ _ _
                                           _ f _
                                           _ _ _
                                           """);

        BotCardTargeting.BestBloodhoundCentre(board, 3).Should().Be(BotCardTargeting.None);
    }

    [Fact]
    public void MinefieldScout_PicksTheLineWithMostClosedCells()
    {
        // The only closed line: the horizontal in the third row.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ t t t t t _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _
                                           """);

        var centre = BotCardTargeting.BestMinefieldScoutCentre(board, 4);

        centre.y.Should().Be(2);
        PatternShapes.Line(4, horizontal: true).SelectTaken(board, centre).Should().HaveCount(4);
    }

    [Fact]
    public void Trebuchet_CoversTheLargestOpenArea()
    {
        // Only the top-left square is open: the diamond must sit on it, not on the closed field.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ t t t t
                                           _ _ _ t t t t
                                           _ _ _ t t t t
                                           t t t t t t t
                                           t t t t t t t
                                           t t t t t t t
                                           t t t t t t t
                                           """);

        var centre = BotCardTargeting.BestTrebuchetCentre(board, 4);

        var covered = PatternShapes.Rhombus(4).SelectFree(board, centre);
        covered.Should().HaveCountGreaterThan(4);
        covered.Should().OnlyContain(c => c.Position.x <= 2 && c.Position.y <= 2);
    }

    [Fact]
    public void Trebuchet_NoOpenCells_ReturnsNone()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);

        BotCardTargeting.BestTrebuchetCentre(board, 4).Should().Be(BotCardTargeting.None);
    }

    [Fact]
    public void Sonar_AimsWhereNumbersPromiseMines()
    {
        // On the left a number 2 with two closed neighbors (both are mines by the constraint),
        // on the right closed cells with no number nearby. The diamond must go left.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _ _ t
                                           m _ _ _ _ _ t
                                           m _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           _ _ _ _ _ _ t
                                           """);

        var centre = BotCardTargeting.BestSonarCentre(board, 4);

        var covered = PatternShapes.Rhombus(4).SelectTaken(board, centre);
        covered.Should().Contain(c => c.Position == new Position(0, 1));
        covered.Should().Contain(c => c.Position == new Position(0, 2));
        covered.Should().NotContain(c => c.Position.x == 6);
    }

    [Fact]
    public void Sonar_NoNumbersWithUnresolvedMines_ReturnsNone()
    {
        // The only mine is already under a flag: the numbers are closed, Sonar has nothing to hit.
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ t
                                           _ f _ t
                                           _ _ _ t
                                           _ _ _ t
                                           """);

        BotCardTargeting.BestSonarCentre(board, 4).Should().Be(BotCardTargeting.None);
        BotCardTargeting.UnresolvedMinesByNumbers(board).Should().Be(0);
    }
}
