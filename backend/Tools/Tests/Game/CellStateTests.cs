using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

public class CellStateTests {
    private class TestEffect : ICellEffect {
        public TestEffect(CellEffectType type) {
            Id = Guid.NewGuid();
            Type = type;
        }

        public Guid Id { get; }
        public CellEffectType Type { get; }
    }

    [Fact]
    public void TakenCell_ToFree_CreatesFreeCell() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.ToFree();

        board.Cells[pos].Status.Should().Be(CellStatus.Free);
    }

    [Fact]
    public void FreeCell_ToTaken_CreatesTakenCell() {
        var board = new TestBoardBuilder(3)
            .WithFreeAt((1, 1))
            .Build();
        var pos = new Position(1, 1);

        var free = (IFreeCell)board.Cells[pos];
        free.ToTaken();

        board.Cells[pos].Status.Should().Be(CellStatus.Taken);
    }

    [Fact]
    public void TakenCell_ToTaken_ReturnsSelf() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        var result = taken.ToTaken();

        ReferenceEquals(result, taken).Should().BeTrue();
    }

    [Fact]
    public void FreeCell_ToFree_ReturnsSelf() {
        var board = new TestBoardBuilder(3)
            .WithFreeAt((1, 1))
            .Build();
        var pos = new Position(1, 1);

        var free = (IFreeCell)board.Cells[pos];
        var result = free.ToFree();

        ReferenceEquals(result, free).Should().BeTrue();
    }

    [Fact]
    public void TakenCell_SetMine_SetsHasMine() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.SetMine();

        taken.HasMine.Should().BeTrue();
    }

    [Fact]
    public void TakenCell_SetFlag_SetsIsFlagged() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.SetFlag();

        taken.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void TakenCell_RemoveFlag_ClearsFlagged() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        taken.SetFlag();
        taken.RemoveFlag();

        taken.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void TakenCell_DefaultState_NoMineNoFlag() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];

        taken.HasMine.Should().BeFalse();
        taken.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void AddEffect_OnTakenCell() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        var effect = new TestEffect(CellEffectType.Smoke);
        taken.AddEffect(effect);

        taken.Effects.Should().HaveCount(1);
        taken.Effects[0].Id.Should().Be(effect.Id);
    }

    [Fact]
    public void RemoveEffect_OnTakenCell() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        var effect1 = new TestEffect(CellEffectType.Smoke);
        var effect2 = new TestEffect(CellEffectType.Fog);
        taken.AddEffect(effect1);
        taken.AddEffect(effect2);

        taken.RemoveEffect(effect1.Id);

        taken.Effects.Should().HaveCount(1);
        taken.Effects[0].Id.Should().Be(effect2.Id);
    }

    [Fact]
    public void AddEffect_OnFreeCell() {
        var board = new TestBoardBuilder(3)
            .WithFreeAt((1, 1))
            .Build();
        var pos = new Position(1, 1);

        var free = (IFreeCell)board.Cells[pos];
        var effect = new TestEffect(CellEffectType.Smoke);
        free.AddEffect(effect);

        free.Effects.Should().HaveCount(1);
        free.Effects[0].Id.Should().Be(effect.Id);
    }

    [Fact]
    public void RemoveEffect_OnFreeCell() {
        var board = new TestBoardBuilder(3)
            .WithFreeAt((1, 1))
            .Build();
        var pos = new Position(1, 1);

        var free = (IFreeCell)board.Cells[pos];
        var effect1 = new TestEffect(CellEffectType.Smoke);
        var effect2 = new TestEffect(CellEffectType.Fog);
        free.AddEffect(effect1);
        free.AddEffect(effect2);

        free.RemoveEffect(effect1.Id);

        free.Effects.Should().HaveCount(1);
        free.Effects[0].Id.Should().Be(effect2.Id);
    }

    [Fact]
    public void ToFree_UpdatesBoardCellsDictionary() {
        var board = new TestBoardBuilder(3).Build();
        var pos = new Position(1, 1);

        var oldTaken = (ITakenCell)board.Cells[pos];
        var newFree = oldTaken.ToFree();

        board.Cells[pos].Should().BeSameAs(newFree);
        board.Cells[pos].Should().NotBeSameAs(oldTaken);
    }
}
