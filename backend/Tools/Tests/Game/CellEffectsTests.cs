using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Tests cell effect persistence — AddEffect/RemoveEffect on both TakenCell and FreeCell.
/// </summary>
public class CellEffectsTests
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
    public void AddEffect_OnTakenCell_AppearsInEffectsList()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var taken = (ITakenCell)board.Cells[new Position(1, 1)];

        var effect = new TestEffect(CellEffectType.Smoke);
        taken.AddEffect(effect);

        taken.Effects.Should().HaveCount(1);
        taken.Effects[0].Should().BeSameAs(effect);
    }

    [Fact]
    public void AddEffect_OnFreeCell_AppearsInEffectsList()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var free = (IFreeCell)board.Cells[new Position(1, 1)];

        var effect = new TestEffect(CellEffectType.Fog);
        free.AddEffect(effect);

        free.Effects.Should().HaveCount(1);
        free.Effects[0].Should().BeSameAs(effect);
    }

    [Fact]
    public void RemoveEffect_ByGuid_RemovesCorrectEffect()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var taken = (ITakenCell)board.Cells[new Position(1, 1)];

        var effect1 = new TestEffect(CellEffectType.Smoke);
        var effect2 = new TestEffect(CellEffectType.Fog);
        taken.AddEffect(effect1);
        taken.AddEffect(effect2);

        taken.RemoveEffect(effect1.Id);

        taken.Effects.Should().HaveCount(1);
        taken.Effects[0].Id.Should().Be(effect2.Id);
    }

    [Fact]
    public void RemoveEffect_WithUnknownGuid_NoOp()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var taken = (ITakenCell)board.Cells[new Position(1, 1)];

        var effect = new TestEffect(CellEffectType.Smoke);
        taken.AddEffect(effect);

        taken.RemoveEffect(Guid.NewGuid()); // unknown

        taken.Effects.Should().HaveCount(1, "unknown Guid removes nothing");
        taken.Effects[0].Id.Should().Be(effect.Id);
    }

    [Fact]
    public void MultipleEffects_OnSameCell_AllTracked()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var taken = (ITakenCell)board.Cells[new Position(1, 1)];

        var effect1 = new TestEffect(CellEffectType.Smoke);
        var effect2 = new TestEffect(CellEffectType.Fog);
        var effect3 = new TestEffect(CellEffectType.Smoke);

        taken.AddEffect(effect1);
        taken.AddEffect(effect2);
        taken.AddEffect(effect3);

        taken.Effects.Should().HaveCount(3);
        taken.Effects.Select(e => e.Id).Should().Contain(effect1.Id);
        taken.Effects.Select(e => e.Id).Should().Contain(effect2.Id);
        taken.Effects.Select(e => e.Id).Should().Contain(effect3.Id);
    }

    [Fact]
    public void EffectsOnTakenCell_LostOnToFree()
    {
        // ToFree creates a new FreeCell — effects on old TakenCell are not carried over
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var taken = (ITakenCell)board.Cells[pos];
        var effect = new TestEffect(CellEffectType.Smoke);
        taken.AddEffect(effect);
        taken.Effects.Should().HaveCount(1);

        taken.ToFree();

        var free = (IFreeCell)board.Cells[pos];
        free.Effects.Should().BeEmpty("ToFree creates a new FreeCell without effects");
    }

    [Fact]
    public void EffectsOnFreeCell_LostOnToTaken()
    {
        // ToTaken creates a new TakenCell — effects on old FreeCell are not carried over
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var pos = new Position(1, 1);

        var free = (IFreeCell)board.Cells[pos];
        var effect = new TestEffect(CellEffectType.Fog);
        free.AddEffect(effect);
        free.Effects.Should().HaveCount(1);

        free.ToTaken();

        var taken = (ITakenCell)board.Cells[pos];
        taken.Effects.Should().BeEmpty("ToTaken creates a new TakenCell without effects");
    }

    [Fact]
    public void RemoveEffect_OnFreeCell_ByGuid()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var free = (IFreeCell)board.Cells[new Position(1, 1)];

        var effect1 = new TestEffect(CellEffectType.Smoke);
        var effect2 = new TestEffect(CellEffectType.Fog);
        free.AddEffect(effect1);
        free.AddEffect(effect2);

        free.RemoveEffect(effect1.Id);

        free.Effects.Should().HaveCount(1);
        free.Effects[0].Id.Should().Be(effect2.Id);
    }

    [Fact]
    public void RemoveEffect_OnFreeCell_WithUnknownGuid_NoOp()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var free = (IFreeCell)board.Cells[new Position(1, 1)];

        var effect = new TestEffect(CellEffectType.Smoke);
        free.AddEffect(effect);

        free.RemoveEffect(Guid.NewGuid());

        free.Effects.Should().HaveCount(1);
    }

    [Fact]
    public void AddEffect_FiresEvent_OnTakenCell()
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
        var testEffect = new TestEffect(CellEffectType.Smoke);
        taken.AddEffect(testEffect);

        fired.Should().HaveCount(1);
        fired[0].Effect.Id.Should().Be(testEffect.Id);
        fired[0].Effect.Type.Should().Be(CellEffectType.Smoke);
    }

    [Fact]
    public void RemoveEffect_FiresEvent_OnTakenCell()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<(ICell Cell, Guid EffectId)>();

        board.Events.EffectRemoved.Advise(lifetime, (cell, id) => fired.Add((cell, id)));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        var testEffect = new TestEffect(CellEffectType.Fog);
        taken.AddEffect(testEffect);
        taken.RemoveEffect(testEffect.Id);

        fired.Should().HaveCount(1);
        fired[0].EffectId.Should().Be(testEffect.Id);
    }

    [Fact]
    public void RemoveEffect_FiresEvent_EvenForUnknownGuid()
    {
        // RemoveEffect always fires the event, even if no effect was actually removed
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<Guid>();

        board.Events.EffectRemoved.Advise(lifetime, (_, id) => fired.Add(id));

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        var unknownId = Guid.NewGuid();
        taken.RemoveEffect(unknownId);

        fired.Should().HaveCount(1);
        fired[0].Should().Be(unknownId);
    }

    [Fact]
    public void AddEffect_OnFreeCell_FiresEvent()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);
        var lifetime = new Lifetime();
        var fired = new List<ICellEffect>();

        board.Events.EffectAdded.Advise(lifetime, (_, effect) => fired.Add(effect));

        var free = (IFreeCell)board.Cells[new Position(1, 1)];
        var testEffect = new TestEffect(CellEffectType.Smoke);
        free.AddEffect(testEffect);

        fired.Should().HaveCount(1);
        fired[0].Id.Should().Be(testEffect.Id);
    }

    [Fact]
    public void EmptyEffects_ByDefault()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);

        var taken = (ITakenCell)board.Cells[new Position(1, 1)];
        taken.Effects.Should().BeEmpty();
    }

    [Fact]
    public void EmptyEffects_OnFreeCell_ByDefault()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t _ t
                                           t t t
                                           """);

        var free = (IFreeCell)board.Cells[new Position(1, 1)];
        free.Effects.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAllEffects_LeavesEmptyList()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t
                                           t t t
                                           t t t
                                           """);
        var taken = (ITakenCell)board.Cells[new Position(1, 1)];

        var effect1 = new TestEffect(CellEffectType.Smoke);
        var effect2 = new TestEffect(CellEffectType.Fog);
        taken.AddEffect(effect1);
        taken.AddEffect(effect2);

        taken.RemoveEffect(effect1.Id);
        taken.RemoveEffect(effect2.Id);

        taken.Effects.Should().BeEmpty();
    }
}