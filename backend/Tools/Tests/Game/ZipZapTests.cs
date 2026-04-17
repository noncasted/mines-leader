using Cluster.Configs;
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
///
/// Board needs: Free cells in Rhombus(3) AND Taken unflagged mines within Rhombus(4).
/// </summary>
public class ZipZapTests : PlayerCardTestsBase
{
    private static IPlayer MockInvoker(IBoard board, float trebuchetBoost = 0f)
    {
        var player = MockPlayer();
        player.Board.Returns(board);

        var values = new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, trebuchetBoost }
        };
        player.Modifiers.Values.Returns(values);
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

        var invoker = MockInvoker(board);
        var card = new ZipZap(MockConfigs());
        var (result, snapshot) = card.UseCapture(invoker, new CardUsePayload.ZipZap { Position = target });

        result.Result.HasError.Should().BeFalse();

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.ZipZap>();
        actionData.Should().NotBeNull();
        actionData!.TargetCells.Should().Contain(new Position(2, 2));
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

        var invoker = MockInvoker(board);
        var card = new ZipZap(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.ZipZap { Position = target });

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

        var invoker = MockInvoker(board);
        var card = new ZipZap(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.ZipZap { Position = target });

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

        var invoker = MockInvoker(board);
        var card = new ZipZap(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.ZipZap { Position = target });

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

        var invoker = MockInvoker(board);
        var card = new ZipZap(MockConfigs());
        var (result, snapshot) = card.UseCapture(invoker, new CardUsePayload.ZipZap { Position = target });

        result.Result.HasError.Should().BeFalse();

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.ZipZap>();
        actionData.Should().NotBeNull();
        actionData!.TargetCells.Count.Should().Be(2, "both mines at (2,2) and (1,1) should be found");
        actionData.TargetCells.Should().Contain(new Position(2, 2));
        actionData.TargetCells.Should().Contain(new Position(1, 1));
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
        var invoker = MockInvoker(board);
        var card = new ZipZap(MockConfigs());
        var (result, snapshot) = card.UseCapture(invoker, new CardUsePayload.ZipZap { Position = target });

        result.Result.HasError.Should().BeFalse();

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.ZipZap>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
        actionData.TargetCells.Should().NotBeEmpty();
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

        var invoker = MockInvoker(board);
        var card = new ZipZap(MockConfigs());
        var (result, snapshot) = card.UseCapture(invoker, new CardUsePayload.ZipZap { Position = target });

        result.Result.HasError.Should().BeFalse();

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.ZipZap>();

        foreach (var pos in actionData!.TargetCells)
        {
            board.Cells[pos]
                 .Status.Should()
                 .Be(CellStatus.Free,
                     $"target mine at {pos} should be converted to Free");
        }
    }
}