using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// ZipZap: first checks that Free cells exist in Rhombus(Size=3) around target.
/// Then chains through unflagged mines via SearchRadius(4) Rhombus, up to Size targets.
/// Converts each found mine cell to Free, then Reveal flood-fills.
/// Uses snapshot Lock/Unlock to batch cell changes.
///
/// Board needs: Free cells in Rhombus(3) AND Taken unflagged mines within Rhombus(4).
/// </summary>
public class ZipZapTests
{
    private static IPlayer MockOwner(float trebuchetBoost = 0f)
    {
        var player = Substitute.For<IPlayer>();
        var modifiers = Substitute.For<IModifiers>();

        var values = new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, trebuchetBoost }
        };
        modifiers.Values.Returns(values);
        player.Modifiers.Returns(modifiers);
        return player;
    }

    [Fact]
    public void Use_FindsAndOpensMineCells()
    {
        // Free cells adjacent to center (3,3), mine at (2,2) — Taken, in Rhombus(4)
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t m _ t t t t
                                                t t _ x _ t t t
                                                t t t _ t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                """);

        var owner = MockOwner();
        var snapshot = new MoveSnapshot();

        var result = new ZipZap(owner, board, snapshot, CardConfigs.ZipZap,
            new CardUsePayload.ZipZap { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        var actionData = result.ActionData as CardActionSnapshot.ZipZap;
        actionData.Should().NotBeNull();
        actionData!.Targets.Should().Contain(new Position(2, 2));
    }

    [Fact]
    public void Use_NoFreeCellsInPattern_Fails()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t m x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var owner = MockOwner();
        var snapshot = new MoveSnapshot();

        var result = new ZipZap(owner, board, snapshot, CardConfigs.ZipZap,
            new CardUsePayload.ZipZap { Position = target }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_NoUnflaggedMines_Fails()
    {
        // Free cells exist but no mines
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t t _ t t t t
                                                t t _ x _ t t t
                                                t t t _ t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                """);

        var owner = MockOwner();
        var snapshot = new MoveSnapshot();

        var result = new ZipZap(owner, board, snapshot, CardConfigs.ZipZap,
            new CardUsePayload.ZipZap { Position = target }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_FlaggedMinesSkipped()
    {
        // Free cells + only flagged mine nearby
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t f _ t t t t
                                                t t _ x _ t t t
                                                t t t _ t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                """);

        var owner = MockOwner();
        var snapshot = new MoveSnapshot();

        var result = new ZipZap(owner, board, snapshot, CardConfigs.ZipZap,
            new CardUsePayload.ZipZap { Position = target }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ChainMultipleMines()
    {
        // Mine at (2,2) and another at (1,1) — chain from first to second
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t t
                                                t m t t t t t t
                                                t t m _ t t t t
                                                t t _ x _ t t t
                                                t t t _ t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                """);

        var owner = MockOwner();
        var snapshot = new MoveSnapshot();

        var result = new ZipZap(owner, board, snapshot, CardConfigs.ZipZap,
            new CardUsePayload.ZipZap { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        var actionData = result.ActionData as CardActionSnapshot.ZipZap;
        actionData.Should().NotBeNull();
        actionData!.Targets.Count.Should().Be(2, "both mines at (2,2) and (1,1) should be found");
        actionData.Targets.Should().Contain(new Position(2, 2));
        actionData.Targets.Should().Contain(new Position(1, 1));
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayerAndPositions()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t m _ t t t t
                                                t t _ x _ t t t
                                                t t t _ t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                """);

        var ownerId = board.OwnerId;
        var owner = MockOwner();
        var snapshot = new MoveSnapshot();

        var result = new ZipZap(owner, board, snapshot, CardConfigs.ZipZap,
            new CardUsePayload.ZipZap { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        var actionData = result.ActionData as CardActionSnapshot.ZipZap;
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
        actionData.Targets.Should().NotBeEmpty();
    }

    [Fact]
    public void Use_TargetCellsConvertedToFree()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t m _ t t t t
                                                t t _ x _ t t t
                                                t t t _ t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                t t t t t t t t
                                                """);

        var owner = MockOwner();
        var snapshot = new MoveSnapshot();

        var result = new ZipZap(owner, board, snapshot, CardConfigs.ZipZap,
            new CardUsePayload.ZipZap { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        var actionData = result.ActionData as CardActionSnapshot.ZipZap;

        foreach (var pos in actionData!.Targets)
        {
            board.Cells[pos]
                 .Status.Should()
                 .Be(CellStatus.Free,
                     $"target mine at {pos} should be converted to Free");
        }
    }
}