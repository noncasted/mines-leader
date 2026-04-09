using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Sonar (Size=4 from config): auto-flags unflagged mines in rhombus(4) pattern.
/// Rhombus(4) is even → effective reach is small (~1-2 cells from target).
/// Does NOT modify cell status (Taken stays Taken), only sets IsFlagged on mine cells.
/// </summary>
public class SonarTests
{
    [Fact]
    public void Use_FlagsMineInRange()
    {
        // Mine at (3,3) — within rhombus(4) reach of target (4,4)
        var (board, target) = BoardParser.Parse("""
                                                t t t t t m t t t t
                                                t t m t t t t t t t
                                                t t t t t t t m t t
                                                t t t m t t t t t t
                                                t m t t x t t t t t
                                                t t t t t t t t m t
                                                t t t t t t m t t t
                                                t t m t t t t t t t
                                                t t t t t t t t t t
                                                t t t t m t t t m t
                                                """);

        var result = new Sonar(board, CardConfigs.Sonar,
            new CardUsePayload.Sonar { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        // Only (3,3) is close enough for rhombus(4)
        BoardParser.AssertBoard(board, """
                                       t t t t t m t t t t
                                       t t m t t t t t t t
                                       t t t t t t t m t t
                                       t t t f t t t t t t
                                       t m t t t t t t t t
                                       t t t t t t t t m t
                                       t t t t t t m t t t
                                       t t m t t t t t t t
                                       t t t t t t t t t t
                                       t t t t m t t t m t
                                       """);
    }

    [Fact]
    public void Use_MultipleMinesInRange()
    {
        // Place mines close to target on all sides
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t m t t
                                                t m x m t
                                                t t m t t
                                                t t t t t
                                                """);

        var result = new Sonar(board, CardConfigs.Sonar,
            new CardUsePayload.Sonar { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        BoardParser.AssertBoard(board, """
                                       t t t t t
                                       t t f t t
                                       t f t f t
                                       t t f t t
                                       t t t t t
                                       """);
    }

    [Fact]
    public void Use_SkipsAlreadyFlaggedMines()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t f t t
                                                t t x t t
                                                t t m t t
                                                t t t t t
                                                """);

        var result = new Sonar(board, CardConfigs.Sonar,
            new CardUsePayload.Sonar { Position = target }).Use();

        var snapshot = result.ActionData as CardActionSnapshot.Sonar;
        snapshot!.FlaggedCells.Should().HaveCount(1);
        snapshot.FlaggedCells.Should().Contain(new Position(2, 3));
    }

    [Fact]
    public void Use_DistantMines_NotFlagged()
    {
        // Mines far from center — outside rhombus(4) range
        var (board, target) = BoardParser.Parse("""
                                                m t t t t t t t m
                                                t t t t t t t t t
                                                t t t t t t t t t
                                                t t t t x t t t t
                                                t t t t t t t t t
                                                t t t t t t t t t
                                                t t t t t t t t t
                                                t t t t t t t t t
                                                m t t t t t t t m
                                                """);

        var result = new Sonar(board, CardConfigs.Sonar,
            new CardUsePayload.Sonar { Position = target }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_NoMines_Fails()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var result = new Sonar(board, CardConfigs.Sonar,
            new CardUsePayload.Sonar { Position = target }).Use();

        result.Result.HasError.Should().BeTrue();
    }
}