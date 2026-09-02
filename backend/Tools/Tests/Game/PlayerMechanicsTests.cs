using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class HealthTests
{
    private static Health Create()
    {
        var modifiers = new Modifiers();
        return new Health(modifiers);
    }

    [Fact]
    public void SetMax_SetsMaximumHp()
    {
        var health = Create();

        health.SetMax(new MoveSnapshot(), 100);

        health.BaseMax.Should().Be(100);
        health.ResultMax.Should().Be(100);
    }

    [Fact]
    public void SetMax_ClampsCurrent_WhenCurrentExceedsNewMax()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 100);
        health.SetCurrent(new MoveSnapshot(), 100);

        health.SetMax(new MoveSnapshot(), 40);

        health.Current.Should().Be(40);
        health.BaseMax.Should().Be(40);
        health.ResultMax.Should().Be(40);
    }

    [Fact]
    public void AdditionalHealth_IncreasesResultMaxNotBaseMax()
    {
        var modifiers = new Modifiers();
        var health = new Health(modifiers);
        health.SetMax(new MoveSnapshot(), 3);
        health.SetCurrent(new MoveSnapshot(), 3);

        modifiers.Add(new MoveSnapshot(), new DurationModifierSource(PlayerModifier.AdditionalHealth, 2f, "", -1));

        health.Current.Should().Be(3);
        health.BaseMax.Should().Be(3);
        health.ResultMax.Should().Be(5);
    }

    [Fact]
    public void SetCurrent_ClampsToMax()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 50);

        health.SetCurrent(new MoveSnapshot(), 999);

        health.Current.Should().Be(50);
    }

    [Fact]
    public void SetCurrent_ClampsToZero()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 50);

        health.SetCurrent(new MoveSnapshot(), -10);

        health.Current.Should().Be(0);
    }

    [Fact]
    public void SetCurrent_WithinRange_SetsExactValue()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 100);

        health.SetCurrent(new MoveSnapshot(), 42);

        health.Current.Should().Be(42);
    }

    [Fact]
    public void TakeDamage_ReducesCurrentHp()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 100);
        health.SetCurrent(new MoveSnapshot(), 100);

        health.TakeDamage(new MoveSnapshot(), 30);

        health.Current.Should().Be(70);
    }

    [Fact]
    public void TakeDamage_BelowZero_ClampsToZero()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 100);
        health.SetCurrent(new MoveSnapshot(), 10);

        health.TakeDamage(new MoveSnapshot(), 50);

        health.Current.Should().Be(0);
    }

    [Fact]
    public void TakeDamage_NegativeAmount_Throws()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 100);
        health.SetCurrent(new MoveSnapshot(), 50);

        var act = () => health.TakeDamage(new MoveSnapshot(), -5);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Heal_IncreasesCurrentHp()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 100);
        health.SetCurrent(new MoveSnapshot(), 50);

        health.Heal(new MoveSnapshot(), 20);

        health.Current.Should().Be(70);
    }

    [Fact]
    public void Heal_AboveMax_ClampsToMax()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 100);
        health.SetCurrent(new MoveSnapshot(), 90);

        health.Heal(new MoveSnapshot(), 50);

        health.Current.Should().Be(100);
    }

    [Fact]
    public void Heal_NegativeAmount_Throws()
    {
        var health = Create();

        var act = () => health.Heal(new MoveSnapshot(), -1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TakeDamage_ToZero_CurrentIsZero()
    {
        var health = Create();
        health.SetMax(new MoveSnapshot(), 50);
        health.SetCurrent(new MoveSnapshot(), 50);

        health.TakeDamage(new MoveSnapshot(), 50);

        health.Current.Should().Be(0);
    }

    [Fact]
    public void Updated_FiresOnStateChange()
    {
        var health = Create();
        var lifetime = new Lifetime();
        var fireCount = 0;

        health.Updated.Advise(lifetime, () => fireCount++);
        health.SetMax(new MoveSnapshot(), 80);
        health.SetCurrent(new MoveSnapshot(), 45);

        fireCount.Should().Be(2);
        health.BaseMax.Should().Be(80);
        health.ResultMax.Should().Be(80);
        health.Current.Should().Be(45);
        lifetime.Terminate();
    }
}

public class ManaTests
{
    private static Mana Create()
    {
        return new Mana(Substitute.For<IModifiers>());
    }

    [Fact]
    public void SetMax_SetsMaximumMana()
    {
        var mana = Create();

        mana.SetMax(new MoveSnapshot(), 10);

        mana.ResultMax.Should().Be(10);
    }

    [Fact]
    public void SetMax_ClampsCurrent_WhenCurrentExceedsNewMax()
    {
        var mana = Create();
        mana.SetMax(new MoveSnapshot(), 10);
        mana.Restore(new MoveSnapshot());
        mana.Current.Should().Be(10);

        mana.SetMax(new MoveSnapshot(), 5);

        mana.Current.Should().Be(5);
    }

    [Fact]
    public void Restore_SetsCurrentToMax()
    {
        var mana = Create();
        mana.SetMax(new MoveSnapshot(), 10);
        mana.Use(new MoveSnapshot(), 5);

        mana.Restore(new MoveSnapshot());

        mana.Current.Should().Be(10);
    }

    [Fact]
    public void Use_ReducesCurrentMana()
    {
        var mana = Create();
        mana.SetMax(new MoveSnapshot(), 10);
        mana.Restore(new MoveSnapshot());

        mana.Use(new MoveSnapshot(), 3);

        mana.Current.Should().Be(7);
    }

    [Fact]
    public void Use_MoreThanAvailable_ClampsToZero()
    {
        var mana = Create();
        mana.SetMax(new MoveSnapshot(), 5);
        mana.Restore(new MoveSnapshot());

        mana.Use(new MoveSnapshot(), 20);

        mana.Current.Should().Be(0);
    }

    [Fact]
    public void Use_NegativeAmount_Throws()
    {
        var mana = Create();

        var act = () => mana.Use(new MoveSnapshot(), -1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCurrent_ClampsToMax()
    {
        var mana = Create();
        mana.SetMax(new MoveSnapshot(), 10);

        mana.SetCurrent(new MoveSnapshot(), 50);

        mana.Current.Should().Be(10);
    }

    [Fact]
    public void SetCurrent_ClampsToZero()
    {
        var mana = Create();
        mana.SetMax(new MoveSnapshot(), 10);

        mana.SetCurrent(new MoveSnapshot(), -5);

        mana.Current.Should().Be(0);
    }

    [Fact]
    public void Updated_FiresOnStateChange()
    {
        var mana = Create();
        var lifetime = new Lifetime();
        var fireCount = 0;

        mana.Updated.Advise(lifetime, () => fireCount++);
        mana.SetMax(new MoveSnapshot(), 8);
        mana.Restore(new MoveSnapshot());
        mana.Use(new MoveSnapshot(), 3);

        fireCount.Should().Be(3);
        mana.ResultMax.Should().Be(8);
        mana.Current.Should().Be(5);
        lifetime.Terminate();
    }
}

public class MovesTests
{
    private static Moves Create()
    {
        var modifiers = new Modifiers();
        return new Moves(modifiers);
    }

    [Fact]
    public void SetMax_SetsMaxMoves()
    {
        var moves = Create();

        moves.SetMax(new MoveSnapshot(), 5);

        moves.ResultMax.Should().Be(5);
    }

    [Fact]
    public void Restore_ResetsLeftToMax()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 3);

        moves.Restore(new MoveSnapshot());

        moves.Left.Should().Be(3);
    }

    [Fact]
    public void OnUsed_DecrementsByOne()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 3);
        moves.Restore(new MoveSnapshot());

        moves.OnUsed(new MoveSnapshot());

        moves.Left.Should().Be(2);
    }

    [Fact]
    public void OnUsed_AtZero_Throws()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 1);
        moves.Restore(new MoveSnapshot());
        moves.OnUsed(new MoveSnapshot());

        var act = () => moves.OnUsed(new MoveSnapshot());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Lock_SetsLeftToZero()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 5);
        moves.Restore(new MoveSnapshot());

        moves.Lock(new MoveSnapshot());

        moves.Left.Should().Be(0);
        moves.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Restore_SetsIsAvailableTrue()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 3);
        moves.Lock(new MoveSnapshot());

        moves.Restore(new MoveSnapshot());

        moves.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void SetCurrent_ClampsToMax()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 5);

        moves.SetCurrent(new MoveSnapshot(), 100);

        moves.Left.Should().Be(5);
    }

    [Fact]
    public void SetCurrent_ClampsToZero()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 5);

        moves.SetCurrent(new MoveSnapshot(), -10);

        moves.Left.Should().Be(0);
    }

    [Fact]
    public void SetMax_ClampsLeftWhenExceeds()
    {
        var moves = Create();
        moves.SetMax(new MoveSnapshot(), 10);
        moves.Restore(new MoveSnapshot());

        moves.SetMax(new MoveSnapshot(), 3);

        moves.Left.Should().Be(3);
    }

    [Fact]
    public void Updated_FiresOnStateChange()
    {
        var moves = Create();
        var lifetime = new Lifetime();
        var fireCount = 0;

        moves.Updated.Advise(lifetime, () => fireCount++);
        moves.SetMax(new MoveSnapshot(), 7);
        moves.Restore(new MoveSnapshot());

        fireCount.Should().Be(2);
        moves.ResultMax.Should().Be(7);
        moves.Left.Should().Be(7);
        lifetime.Terminate();
    }
}

public class DeckTests
{
    private static (Deck deck, ValueProperty<PlayerDeckState> state) Create(IReadOnlyList<CardType>? selected = null)
    {
        var state = new ValueProperty<PlayerDeckState>(0).ForTest();
        state.Update(s => s.Queue = new List<CardType>());
        selected ??= new[] { CardType.Bloodhound, CardType.Trebuchet };
        var deck = new Deck(selected);
        return (deck, state);
    }

    [Fact]
    public void Init_FillsDeckWithCards()
    {
        var (deck, _) = Create();

        deck.Init(6);

        deck.Count.Should().Be(6);
    }

    [Fact]
    public void Init_CyclesThroughSelectedTypes()
    {
        var selected = new[] { CardType.Bloodhound, CardType.Trebuchet };
        var (deck, state) = Create(selected);

        deck.Init(4);

        // Cards are shuffled, so just verify count and types present
        deck.Count.Should().Be(4);
        var drawn = new List<CardType>();

        for (var i = 0; i < 4; i++)
            drawn.Add(deck.DrawCard());

        drawn.Count(c => c == CardType.Bloodhound).Should().Be(2);
        drawn.Count(c => c == CardType.Trebuchet).Should().Be(2);
    }

    [Fact]
    public void DrawCard_ReturnsCardAndRemovesFromDeck()
    {
        var (deck, _) = Create();
        deck.AddCard(CardType.Bloodhound);
        deck.AddCard(CardType.Trebuchet);

        var card = deck.DrawCard();

        card.Should().Be(CardType.Bloodhound);
        deck.Count.Should().Be(1);
    }

    [Fact]
    public void DrawCard_FromEmptyDeck_Throws()
    {
        var (deck, _) = Create();

        var act = () => deck.DrawCard();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddCard_IncreasesCount()
    {
        var (deck, _) = Create();

        deck.AddCard(CardType.Bloodhound);
        deck.AddCard(CardType.Trebuchet);

        deck.Count.Should().Be(2);
    }

    [Fact]
    public void RemoveCard_DecreasesCount()
    {
        var (deck, _) = Create();
        deck.AddCard(CardType.Bloodhound);
        deck.AddCard(CardType.Trebuchet);

        deck.RemoveCard(CardType.Bloodhound);

        deck.Count.Should().Be(1);
    }

    [Fact]
    public void Count_ReflectsCurrentSize()
    {
        var (deck, _) = Create();

        deck.Count.Should().Be(0);
        deck.AddCard(CardType.Bloodhound);
        deck.Count.Should().Be(1);
        deck.DrawCard();
        deck.Count.Should().Be(0);
    }
}

public class HandTests
{
    private static (Hand hand, ValueProperty<PlayerHandState> state) Create()
    {
        var state = new ValueProperty<PlayerHandState>(0).ForTest();
        var hand = new Hand();
        return (hand, state);
    }

    [Fact]
    public void SetSize_SetsHandCapacity()
    {
        var (hand, _) = Create();

        hand.SetSize(5);

        hand.Size.Should().Be(5);
    }

    [Fact]
    public void Add_ReturnsActiveCardWithIdAndType()
    {
        var (hand, _) = Create();

        var card = hand.Add(CardType.Bloodhound);

        card.Id.Should().NotBe(Guid.Empty);
        card.Type.Should().Be(CardType.Bloodhound);
    }

    [Fact]
    public void Add_AppearsInEntries()
    {
        var (hand, _) = Create();

        var card = hand.Add(CardType.Trebuchet);

        hand.Entries.Should().ContainSingle(c => c.Id == card.Id);
    }

    [Fact]
    public void Remove_ById_RemovesCard()
    {
        var (hand, _) = Create();
        var card1 = hand.Add(CardType.Bloodhound);
        var card2 = hand.Add(CardType.Trebuchet);

        hand.Remove(card1.Id);

        hand.Entries.Should().HaveCount(1);
        hand.Entries[0].Id.Should().Be(card2.Id);
    }

    [Fact]
    public void Remove_NonExistentId_DoesNothing()
    {
        var (hand, _) = Create();
        hand.Add(CardType.Bloodhound);

        hand.Remove(Guid.NewGuid());

        hand.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void Entries_ReturnsCurrentHand()
    {
        var (hand, _) = Create();
        var card1 = hand.Add(CardType.Bloodhound);
        var card2 = hand.Add(CardType.Trebuchet);
        var card3 = hand.Add(CardType.ErosionDozer);

        hand.Entries.Should().HaveCount(3);

        hand.Entries.Select(c => c.Type)
            .Should()
            .ContainInOrder(CardType.Bloodhound, CardType.Trebuchet, CardType.ErosionDozer);

        hand.Entries.Select(c => c.Id)
            .Should()
            .ContainInOrder(card1.Id, card2.Id, card3.Id);
    }

    [Fact]
    public void Add_MultipleCards_GeneratesUniqueIds()
    {
        var (hand, _) = Create();

        var card1 = hand.Add(CardType.Bloodhound);
        var card2 = hand.Add(CardType.Bloodhound);

        card1.Id.Should().NotBe(card2.Id);
    }
}

public class StashTests
{
    private static (Stash stash, ValueProperty<PlayerStashState> state) Create()
    {
        var state = new ValueProperty<PlayerStashState>(0).ForTest();
        var stash = new Stash();
        return (stash, state);
    }

    [Fact]
    public void Add_IncreasesCount()
    {
        var (stash, _) = Create();

        stash.Add(CardType.Bloodhound);

        stash.Count.Should().Be(1);
    }

    [Fact]
    public void Pick_ReturnsLastAdded_Lifo()
    {
        var (stash, _) = Create();
        stash.Add(CardType.Bloodhound);
        stash.Add(CardType.Trebuchet);

        var card = stash.Pick();

        card.Should().Be(CardType.Trebuchet);
        stash.Count.Should().Be(1);
    }

    [Fact]
    public void Pick_FromEmptyStash_Throws()
    {
        var (stash, _) = Create();

        var act = () => stash.Pick();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Collect_ReturnsAllAndClears()
    {
        var (stash, _) = Create();
        stash.Add(CardType.Bloodhound);
        stash.Add(CardType.Trebuchet);
        stash.Add(CardType.ErosionDozer);

        var collected = stash.Collect();

        collected.Should().HaveCount(3);
        collected[0].Should().Be(CardType.Bloodhound);
        collected[1].Should().Be(CardType.Trebuchet);
        collected[2].Should().Be(CardType.ErosionDozer);
        stash.Count.Should().Be(0);
    }

    [Fact]
    public void Collect_FromEmptyStash_ReturnsEmpty()
    {
        var (stash, _) = Create();

        var collected = stash.Collect();

        collected.Should().BeEmpty();
        stash.Count.Should().Be(0);
    }
}

public class RoundActionServiceTests
{
    private class TestAction : IRoundAction
    {
        private int _roundsLeft;
        public int ExecuteCount { get; private set; }

        public TestAction(int roundsLeft = 1, Guid ownerId = default)
        {
            _roundsLeft = roundsLeft;
            OwnerId = ownerId;
        }

        public Guid OwnerId { get; }

        public bool Tick(MoveSnapshot snapshot)
        {
            if (_roundsLeft > 0)
                _roundsLeft--;

            var expired = _roundsLeft == 0;
            if (expired)
                ExecuteCount++;

            return expired;
        }
    }

    [Fact]
    public void Schedule_WithDelay_ExecutesAfterNTicks()
    {
        var service = new RoundActionService();
        var action = new TestAction(3);

        service.Schedule(action);

        service.Tick(new MoveSnapshot(), Guid.Empty);
        action.ExecuteCount.Should().Be(0);
        service.Tick(new MoveSnapshot(), Guid.Empty);
        action.ExecuteCount.Should().Be(0);
        service.Tick(new MoveSnapshot(), Guid.Empty);
        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Schedule_WithOneRound_ExecutesOnFirstTick()
    {
        var service = new RoundActionService();
        var action = new TestAction(1);

        service.Schedule(action);
        service.Tick(new MoveSnapshot(), Guid.Empty);

        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Schedule_WithZeroRounds_ExecutesImmediately()
    {
        var service = new RoundActionService();
        var action = new TestAction(0);

        service.Schedule(action);
        service.Tick(new MoveSnapshot(), Guid.Empty);

        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Schedule_NegativeRounds_NeverExecutes()
    {
        var service = new RoundActionService();
        var action = new TestAction(-5);

        service.Schedule(action);
        service.Tick(new MoveSnapshot(), Guid.Empty);

        action.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public void Tick_WithNoActions_DoesNotThrow()
    {
        var service = new RoundActionService();

        var act = () => service.Tick(new MoveSnapshot(), Guid.Empty);

        act.Should().NotThrow();

        // Verify idempotency — ticking twice with no actions is also safe
        service.Tick(new MoveSnapshot(), Guid.Empty);
        service.Tick(new MoveSnapshot(), Guid.Empty);

        // Schedule an action after empty ticks to confirm service is still functional
        var action = new TestAction(1);
        service.Schedule(action);
        service.Tick(new MoveSnapshot(), Guid.Empty);
        action.ExecuteCount.Should().Be(1, "service should still work after empty ticks");
    }

    [Fact]
    public void Tick_OtherPlayersTurn_DoesNotTickAction()
    {
        var service = new RoundActionService();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var action = new TestAction(1, owner);

        service.Schedule(action);

        // Ход закончил не владелец действия — длительность не должна сгорать.
        service.Tick(new MoveSnapshot(), other);
        action.ExecuteCount.Should().Be(0);

        service.Tick(new MoveSnapshot(), owner);
        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Tick_MultipleActionsAtSameRound_AllExecute()
    {
        var service = new RoundActionService();
        var action1 = new TestAction(2);
        var action2 = new TestAction(2);

        service.Schedule(action1);
        service.Schedule(action2);

        service.Tick(new MoveSnapshot(), Guid.Empty);
        action1.ExecuteCount.Should().Be(0);
        action2.ExecuteCount.Should().Be(0);

        service.Tick(new MoveSnapshot(), Guid.Empty);
        action1.ExecuteCount.Should().Be(1);
        action2.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Action_RemovedAfterExecution()
    {
        var service = new RoundActionService();
        var action = new TestAction(1);

        service.Schedule(action);
        service.Tick(new MoveSnapshot(), Guid.Empty);
        service.Tick(new MoveSnapshot(), Guid.Empty);

        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void MultipleActions_DifferentDelays_ExecuteAtCorrectTimes()
    {
        var service = new RoundActionService();
        var early = new TestAction(1);
        var late = new TestAction(3);

        service.Schedule(early);
        service.Schedule(late);

        service.Tick(new MoveSnapshot(), Guid.Empty);
        early.ExecuteCount.Should().Be(1);
        late.ExecuteCount.Should().Be(0);

        service.Tick(new MoveSnapshot(), Guid.Empty);
        late.ExecuteCount.Should().Be(0);

        service.Tick(new MoveSnapshot(), Guid.Empty);
        late.ExecuteCount.Should().Be(1);
    }
}

public class ModifiersTests
{
    private static (Modifiers modifiers, ValueProperty<PlayerModifiersState> state) Create()
    {
        var state = new ValueProperty<PlayerModifiersState>(0).ForTest();
        var modifiers = new Modifiers();
        return (modifiers, state);
    }

    [Fact]
    public void InitialValues_AllZero()
    {
        var (modifiers, _) = Create();

        foreach (var type in PlayerModifierExtensions.All)
            modifiers.Values[type].Should().Be(0f);
    }

    [Fact]
    public void Set_UpdatesValue()
    {
        var (modifiers, _) = Create();

        modifiers.Add(new MoveSnapshot(), new DurationModifierSource(PlayerModifier.TrebuchetBoost, 5f, "", -1));

        modifiers.Values[PlayerModifier.TrebuchetBoost].Should().Be(5f);
    }

    [Fact]
    public void Get_ReturnsCurrentValue()
    {
        var (modifiers, _) = Create();
        modifiers.Add(new MoveSnapshot(), new DurationModifierSource(PlayerModifier.TrebuchetBoost, 3.5f, "", -1));

        var value = modifiers.Get(PlayerModifier.TrebuchetBoost);

        value.Should().Be(3.5f);
    }

    [Fact]
    public void Inc_IncrementsByOne()
    {
        var (modifiers, _) = Create();

        modifiers.Inc(new MoveSnapshot(), PlayerModifier.TrebuchetBoost, 1f);
        modifiers.Inc(new MoveSnapshot(), PlayerModifier.TrebuchetBoost, 1f);

        modifiers.Values[PlayerModifier.TrebuchetBoost].Should().Be(2f);
    }

    [Fact]
    public void Reset_SetsToZero()
    {
        var (modifiers, _) = Create();
        modifiers.Add(new MoveSnapshot(), new DurationModifierSource(PlayerModifier.TrebuchetBoost, 10f, "", -1));

        modifiers.Reset(new MoveSnapshot(), PlayerModifier.TrebuchetBoost);

        modifiers.Values[PlayerModifier.TrebuchetBoost].Should().Be(0f);
    }

    [Fact]
    public void Add_MultipleSourcesSameType_SumsValues()
    {
        var (modifiers, _) = Create();
        var snapshot = new MoveSnapshot();

        modifiers.Add(snapshot, new DurationModifierSource(PlayerModifier.AdditionalMoves, 2f, "a", 1));
        modifiers.Add(snapshot, new DurationModifierSource(PlayerModifier.AdditionalMoves, 3f, "b", 1));

        modifiers.Values[PlayerModifier.AdditionalMoves].Should().Be(5f);
    }

    [Fact]
    public void RemoveOne_SingleSource_RemovesIt()
    {
        var (modifiers, _) = Create();
        var snapshot = new MoveSnapshot();

        modifiers.Add(snapshot, new DurationModifierSource(PlayerModifier.Shield, 1f, "", -1));
        modifiers.Values[PlayerModifier.Shield].Should().Be(1f);

        modifiers.RemoveOne(snapshot, PlayerModifier.Shield);
        modifiers.Values[PlayerModifier.Shield].Should().Be(0f);
    }

    [Fact]
    public void RemoveOne_MultipleSources_RemovesOnlyOne()
    {
        var (modifiers, _) = Create();
        var snapshot = new MoveSnapshot();

        modifiers.Add(snapshot, new DurationModifierSource(PlayerModifier.AdditionalMoves, 2f, "a", 1));
        modifiers.Add(snapshot, new DurationModifierSource(PlayerModifier.AdditionalMoves, 3f, "b", 1));

        modifiers.RemoveOne(snapshot, PlayerModifier.AdditionalMoves);
        modifiers.Values[PlayerModifier.AdditionalMoves].Should().Be(3f);
    }

    [Fact]
    public void RemoveOne_NoSource_DoesNothing()
    {
        var (modifiers, _) = Create();
        var snapshot = new MoveSnapshot();

        var act = () => modifiers.RemoveOne(snapshot, PlayerModifier.Shield);
        act.Should().NotThrow();
        modifiers.Values[PlayerModifier.Shield].Should().Be(0f);
    }
}

public class PlayerActionsTests
{
    [Fact]
    public void OnCellOpened_FiresCellOpenedDelegate()
    {
        var actions = new PlayerActions();
        var lifetime = new Lifetime();
        var fired = false;

        actions.CellOpened.Advise(lifetime, () => fired = true);
        actions.OnCellOpened();

        fired.Should().BeTrue();
        lifetime.Terminate();
    }

    [Fact]
    public void OnCardUsed_FiresCardUsedDelegate()
    {
        var actions = new PlayerActions();
        var lifetime = new Lifetime();
        var fired = false;

        actions.CardUsed.Advise(lifetime, () => fired = true);
        actions.OnCardUsed(CardType.Focus, new CardUsePayload.Focus());

        fired.Should().BeTrue();
        actions.LastUsedCardType.Should().Be(CardType.Focus);
        lifetime.Terminate();
    }

    [Fact]
    public void CellOpened_MultipleListeners_AllFired()
    {
        var actions = new PlayerActions();
        var lifetime = new Lifetime();
        var count = 0;

        actions.CellOpened.Advise(lifetime, () => count++);
        actions.CellOpened.Advise(lifetime, () => count++);
        actions.OnCellOpened();

        count.Should().Be(2);
        lifetime.Terminate();
    }

    [Fact]
    public void CardUsed_AfterLifetimeTerminated_DoesNotFire()
    {
        var actions = new PlayerActions();
        var lifetime = new Lifetime();
        var fired = false;

        actions.CardUsed.Advise(lifetime, () => fired = true);
        lifetime.Terminate();
        actions.OnCardUsed(CardType.Focus, new CardUsePayload.Focus());

        fired.Should().BeFalse();
    }
}