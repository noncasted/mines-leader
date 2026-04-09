using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

public class BoardEventsTests
{
    private class TestEffect : ICellEffect
    {
        public TestEffect(CellEffectType type)
        {
            Id = Guid.NewGuid();
            Type = type;
        }

        public Guid Id { get; }
        public CellEffectType Type { get; }
    }

    [Fact]
    public void CellSet_FiresOnToFree()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICell>();

        board.Events.CellSet.Advise(lifetime, cell => fired.Add(cell));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.ToFree();

        fired.Should().HaveCount(1);
        fired[0].Status.Should().Be(CellStatus.Free);
        fired[0].Position.Should().Be(new Position(1, 1));
    }

    [Fact]
    public void CellSet_FiresOnToTaken()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICell>();

        board.Events.CellSet.Advise(lifetime, cell => fired.Add(cell));

        var free = (IFreeCell)board.Cells[new Position(1, 1)];
        free.ToTaken();

        fired.Should().HaveCount(1);
        fired[0].Status.Should().Be(CellStatus.Taken);
    }

    [Fact]
    public void CellSet_DoesNotFireOnSameTypeTransition_Taken()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICell>();

        board.Events.CellSet.Advise(lifetime, cell => fired.Add(cell));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.ToTaken(); // same type — returns self, no SetCell

        fired.Should().BeEmpty();
    }

    [Fact]
    public void CellSet_DoesNotFireOnSameTypeTransition_Free()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICell>();

        board.Events.CellSet.Advise(lifetime, cell => fired.Add(cell));

        var free = (IFreeCell)board.Cells[new Position(1, 1)];
        free.ToFree(); // same type — returns self, no SetCell

        fired.Should().BeEmpty();
    }

    [Fact]
    public void Flag_FiresOnSetFlag()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<(ICell Cell, bool IsFlagged)>();

        board.Events.Flag.Advise(lifetime, (cell, flagged) => fired.Add((cell, flagged)));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.SetFlag();

        fired.Should().HaveCount(1);
        fired[0].IsFlagged.Should().BeTrue();
        fired[0].Cell.Position.Should().Be(new Position(1, 1));
    }

    [Fact]
    public void Flag_FiresOnRemoveFlag()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t g t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<(ICell Cell, bool IsFlagged)>();

        board.Events.Flag.Advise(lifetime, (cell, flagged) => fired.Add((cell, flagged)));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.RemoveFlag();

        fired.Should().HaveCount(1);
        fired[0].IsFlagged.Should().BeFalse();
    }

    [Fact]
    public void Explode_FiresOnExplode()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t m t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICell>();

        board.Events.Explode.Advise(lifetime, cell => fired.Add(cell));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.Explode();

        fired.Should().HaveCount(1);
        fired[0].Position.Should().Be(new Position(1, 1));
    }

    [Fact]
    public void EffectAdded_FiresOnAddEffect()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<(ICell Cell, ICellEffect Effect)>();

        board.Events.EffectAdded.Advise(lifetime, (cell, effect) => fired.Add((cell, effect)));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        var effect = new TestEffect(CellEffectType.Smoke);
        taken.AddEffect(effect);

        fired.Should().HaveCount(1);
        fired[0].Cell.Position.Should().Be(new Position(1, 1));
        fired[0].Effect.Id.Should().Be(effect.Id);
    }

    [Fact]
    public void EffectRemoved_FiresOnRemoveEffect()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<(ICell Cell, Guid EffectId)>();

        board.Events.EffectRemoved.Advise(lifetime, (cell, effectId) => fired.Add((cell, effectId)));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        var effect = new TestEffect(CellEffectType.Smoke);
        taken.AddEffect(effect);
        taken.RemoveEffect(effect.Id);

        fired.Should().HaveCount(1);
        fired[0].Cell.Position.Should().Be(new Position(1, 1));
        fired[0].EffectId.Should().Be(effect.Id);
    }

    [Fact]
    public void Lock_SuppressesAllEvents()
    {
        var (board, _) = BoardParser.Parse("""
                                           m t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();

        var cellSetFired = 0;
        var flagFired = 0;
        var explodeFired = 0;
        var effectAddedFired = 0;
        var effectRemovedFired = 0;

        board.Events.CellSet.Advise(lifetime, _ => cellSetFired++);
        board.Events.Flag.Advise(lifetime, (_, _) => flagFired++);
        board.Events.Explode.Advise(lifetime, _ => explodeFired++);
        board.Events.EffectAdded.Advise(lifetime, (_, _) => effectAddedFired++);
        board.Events.EffectRemoved.Advise(lifetime, (_, _) => effectRemovedFired++);

        board.Events.Lock();

        // Perform all event-producing operations while locked
        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.ToFree();
        var taken2 = (ITakenCell)board.Cells[new Position(2, 2)];
        taken2.SetFlag();
        var mine = (ITakenCell)board.Cells[new Position(0, 0)];
        mine.Explode();
        var effect = new TestEffect(CellEffectType.Smoke);
        var taken3 = (ITakenCell)board.Cells[new Position(2, 0)];
        taken3.AddEffect(effect);
        taken3.RemoveEffect(effect.Id);

        cellSetFired.Should().Be(0);
        flagFired.Should().Be(0);
        explodeFired.Should().Be(0);
        effectAddedFired.Should().Be(0);
        effectRemovedFired.Should().Be(0);
    }

    [Fact]
    public void Unlock_ResumesEventFiring()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICell>();

        board.Events.CellSet.Advise(lifetime, cell => fired.Add(cell));

        board.Events.Lock();
        var taken1 = (ITakenCell)board.Cells[new Position(0, 0)];
        taken1.ToFree(); // suppressed

        board.Events.Unlock();
        var taken2 = (ITakenCell)board.Cells[new Position(1, 1)];
        taken2.ToFree(); // fires

        fired.Should().HaveCount(1);
        fired[0].Position.Should().Be(new Position(1, 1));
    }

    [Fact]
    public void LockUnlock_IsSimpleBool_NotNested()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICell>();

        board.Events.CellSet.Advise(lifetime, cell => fired.Add(cell));

        // Double lock, single unlock — should resume (simple bool, not ref-counted)
        board.Events.Lock();
        board.Events.Lock();
        board.Events.Unlock();

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.ToFree();

        fired.Should().HaveCount(1, "Unlock sets _isLocked=false regardless of double Lock");
    }

    [Fact]
    public void MinesAround_FiresOnUpdateMinesAround()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<(ICell Cell, int Count)>();

        board.Events.Mines.Advise(lifetime, (cell, count) => fired.Add((cell, count)));

        var free = (IFreeCell)board.Cells[new Position(1, 1)];
        free.UpdateMinesAround(3);

        fired.Should().HaveCount(1);
        fired[0].Count.Should().Be(3);
    }

    [Fact]
    public void MinesAround_DoesNotFireWhenSameValue()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<(ICell Cell, int Count)>();

        board.Events.Mines.Advise(lifetime, (cell, count) => fired.Add((cell, count)));

        var free = (IFreeCell)board.Cells[new Position(1, 1)];
        // MinesAround starts at 0, setting to 0 should not fire
        free.UpdateMinesAround(0);

        fired.Should().BeEmpty();
    }

    [Fact]
    public void ForceRecord_IgnoresLock()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = 0;

        board.Events.Record.Advise(lifetime, _ => fired++);

        board.Events.Lock();
        board.Events.ForceRecord(null!); // ForceRecord bypasses lock

        fired.Should().Be(1);
    }
}