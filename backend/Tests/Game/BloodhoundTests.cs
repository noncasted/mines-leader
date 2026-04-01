using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Bloodhound (Size=3 from config): opens Taken cells in a rhombus(3) cross pattern,
/// then Reveal flood-fills all safe connected cells.
/// Reveal rules: opens cells with MinesAround=0 recursively, stops at mine-adjacent border.
/// </summary>
public class BloodhoundTests {
    [Fact]
    public void Use_OpensTargetAreaAndFloodFills() {
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

        new Bloodhound(board, CardConfigs.Bloodhound,
            new CardUsePayload.Bloodhound { Position = target }).Use();

        // Reveal flood-fills entire safe region bounded by mines t x
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
    public void Use_MinesDenselyPacked_LimitsReveal() {
        // Mines tightly surround target — reveal stays small
        var (board, target) = BoardParser.Parse("""
            t t t t t t t
            t t m m m t t
            t m t t t m t
            t m t x t m t
            t m t t t m t
            t t m m m t t
            t t t t t t t
            """);

        new Bloodhound(board, CardConfigs.Bloodhound,
            new CardUsePayload.Bloodhound { Position = target }).Use();

        // Cross opens (3,2),(2,3),(3,3),(4,3),(3,4) — reveal can't escape mine ring
        BoardParser.AssertBoard(board, """
            t t t t t t t
            t t m m m t t
            t m R R R m t
            t m R R R m t
            t m R R R m t
            t t m m m t t
            t t t t t t t
            """);
    }

    [Fact]
    public void Use_AllTakenAlreadyFreeInCross_Fails() {
        // All cells in rhombus(3) cross are already Free
        var (board, target) = BoardParser.Parse("""
            t t t t t
            t t _ t t
            t _ _ _ t
            t t _ t t
            t t t t t
            """);

        // x position (2,2) is Free already — remap to the center
        var result = new Bloodhound(board, CardConfigs.Bloodhound,
            new CardUsePayload.Bloodhound { Position = new Position(2, 2) }).Use();

        // SelectTaken returns nothing — all cross positions are Free
        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_AtCorner_OpensVisibleArea() {
        var (board, target) = BoardParser.Parse("""
            x t t t t t
            t t t t t t
            t t m t t t
            t t t t m t
            t t t t t t
            t t t t t t
            """);

        new Bloodhound(board, CardConfigs.Bloodhound,
            new CardUsePayload.Bloodhound { Position = target }).Use();

        // Reveal expands from (0,0), stops at mine borders
        // (5,3) is diagonal to mine at (4,3) — MinesAround > 0, no zero-neighbor, stays Taken
        BoardParser.AssertBoard(board, """
            R R R R R R
            R R R R R R
            R R m R R R
            R R R R m t
            R R R R R R
            R R R R R R
            """);
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer() {
        var ownerId = Guid.NewGuid();
        var board = new TestBoardBuilder(5)
            .WithOwner(ownerId)
            .WithMinesAt((0, 0))
            .Build();

        var result = new Bloodhound(board, CardConfigs.Bloodhound,
            new CardUsePayload.Bloodhound { Position = new Position(2, 2) }).Use();

        var snapshot = result.ActionData as CardActionSnapshot.Bloodhound;
        snapshot.Should().NotBeNull();
        snapshot!.TargetPlayer.Should().Be(ownerId);
    }
}
