using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Trebuchet: selects Free cells in a rhombus pattern on opponent's board,
/// converts them all to Taken, then places mines on alternating edge cells per row.
/// TrebuchetBoost modifier increases the effective size by 2 per stack.
/// Resets TrebuchetBoost after use.
/// Config: Size=4, so Rhombus(4) is 4x4, halfSize=2.
/// </summary>
public class TrebuchetTests
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
    public void Use_ConvertsFreeToTaken()
    {
        // Large Free area so Rhombus(4) pattern is fully inside
        var (board, target) = BoardParser.Parse("""
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ x _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                """);

        var freeCountBefore = board.Cells.Values.Count(c => c.Status == CellStatus.Free);

        var owner = MockOwner();

        var result = new Trebuchet(owner, board, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        // Rhombus(4) selects Free cells in a 4x4 diamond and converts them to Taken
        var freeCountAfter = board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var converted = freeCountBefore - freeCountAfter;
        converted.Should().BeGreaterThanOrEqualTo(4, "Rhombus(4) should convert multiple Free cells");
        converted.Should().BeLessThanOrEqualTo(12, "Rhombus(4) has at most 12 cells in the pattern");
    }

    [Fact]
    public void Use_PlacesMinesOnEdgeCells()
    {
        var (board, target) = BoardParser.Parse("""
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ x _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                """);

        var owner = MockOwner();

        var result = new Trebuchet(owner, board, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        // Verify mine placement pattern: first and last cell per Y row
        var mineCells = board.Cells.Values
                             .Where(c => c.Status == CellStatus.Taken && c is ITakenCell t && t.HasMine)
                             .Select(c => c.Position)
                             .ToList();
        mineCells.Should().NotBeEmpty("Trebuchet should place mines on edge cells");

        // Group by Y — each row should have mines only at edge positions
        var minesByRow = mineCells.GroupBy(p => p.y).ToList();
        minesByRow.Should().NotBeEmpty();

        foreach (var row in minesByRow)
        {
            var count = row.Count();

            count.Should()
                 .BeGreaterThanOrEqualTo(1)
                 .And.BeLessThanOrEqualTo(2,
                     $"row y={row.Key} should have 1 or 2 mines (first/last)");
        }
    }

    [Fact]
    public void Use_AllCellsAlreadyTaken_Fails()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var owner = MockOwner();

        var result = new Trebuchet(owner, board, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = target }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_TrebuchetBoostIncreasesSize()
    {
        var (boardNoBoost, _) = BoardParser.Parse("""
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                  """);

        var (boardWithBoost, _) = BoardParser.Parse("""
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    _ _ _ _ _ _ _ _ _ _ _ _ _ _
                                                    """);

        var ownerNoBoost = MockOwner(0f);
        var ownerWithBoost = MockOwner(1f);

        new Trebuchet(ownerNoBoost, boardNoBoost, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = new Position(7, 7) }).Use();

        new Trebuchet(ownerWithBoost, boardWithBoost, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = new Position(7, 7) }).Use();

        var takenNoBoost = boardNoBoost.Cells.Values.Count(c => c.Status == CellStatus.Taken);
        var takenWithBoost = boardWithBoost.Cells.Values.Count(c => c.Status == CellStatus.Taken);

        takenWithBoost.Should()
                      .BeGreaterThan(takenNoBoost,
                          "TrebuchetBoost should increase effective size, converting more cells");
    }

    [Fact]
    public void Use_ResetsTrebuchetBoostAfterUse()
    {
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           """);

        var owner = MockOwner(1f);

        new Trebuchet(owner, board, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = new Position(4, 4) }).Use();

        owner.Modifiers.Received(1).Set(PlayerModifier.TrebuchetBoost, 0f);
    }

    [Fact]
    public void Use_EmptyBoard_Fails()
    {
        var (board, _) = BoardParser.Parse("""
                                           t
                                           """);
        // Use a 0-size board via the parser trick — but actually an empty board has no cells
        // BoardParser always creates at least 1x1, so we use TestBoardBuilder(0) equivalent
        var builder = new TestBoardBuilder(0);
        var emptyBoard = builder.Build();

        var owner = MockOwner();

        var result = new Trebuchet(owner, emptyBoard, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = new Position(0, 0) }).Use();

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var (board, target) = BoardParser.Parse("""
                                                _ _ _ _ _ _
                                                _ _ _ _ _ _
                                                _ _ _ _ _ _
                                                _ _ _ x _ _
                                                _ _ _ _ _ _
                                                _ _ _ _ _ _
                                                """);

        var ownerId = board.OwnerId;
        var owner = MockOwner();

        var result = new Trebuchet(owner, board, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = target }).Use();

        var snapshot = result.ActionData as CardActionSnapshot.Trebuchet;
        snapshot.Should().NotBeNull();
        snapshot!.TargetPlayer.Should().Be(ownerId);
    }

    [Fact]
    public void Use_MinesPlacedOnFirstAndLastPerRow()
    {
        // Verify edge cells pattern: first and last per row in grouped-by-Y
        var (board, target) = BoardParser.Parse("""
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ x _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                """);

        // Track which cells were Free before (those will be the "selected" set)
        var freeBefore = board.Cells.Values
                              .Where(c => c.Status == CellStatus.Free)
                              .Select(c => c.Position)
                              .ToHashSet();

        var owner = MockOwner();

        new Trebuchet(owner, board, CardConfigs.Trebuchet,
            new CardUsePayload.Trebuchet { Position = target }).Use();

        // Find converted cells (were Free, now Taken)
        var converted = board.Cells.Values
                             .Where(c => c.Status == CellStatus.Taken && freeBefore.Contains(c.Position))
                             .ToList();
        converted.Should().NotBeEmpty();

        // Group converted cells by Y, verify first+last per row have mines
        var convertedByY = converted.GroupBy(c => c.Position.y).OrderByDescending(g => g.Key);

        foreach (var group in convertedByY)
        {
            var sorted = group.OrderBy(c => c.Position.x).ToList();
            var first = (ITakenCell)sorted.First();
            first.HasMine.Should().BeTrue($"first cell in row y={group.Key} should have mine");

            if (sorted.Count > 1)
            {
                var last = (ITakenCell)sorted.Last();
                last.HasMine.Should().BeTrue($"last cell in row y={group.Key} should have mine");
            }
        }
    }
}