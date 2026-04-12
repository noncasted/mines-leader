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
    private static (Health health, ValueProperty<PlayerHealthState> state) Create()
    {
        var state = new ValueProperty<PlayerHealthState>(0).ForTest();
        var modifiers = new Modifiers(new ValueProperty<PlayerModifiersState>(0).ForTest());
        var health = new Health(state, modifiers);
        return (health, state);
    }

    [Fact]
    public void SetMax_SetsMaximumHp()
    {
        var (health, _) = Create();

        health.SetMax(100);

        health.Max.Should().Be(100);
    }

    [Fact]
    public void SetCurrent_ClampsToMax()
    {
        var (health, _) = Create();
        health.SetMax(50);

        health.SetCurrent(999);

        health.Current.Value.Should().Be(50);
    }

    [Fact]
    public void SetCurrent_ClampsToZero()
    {
        var (health, _) = Create();
        health.SetMax(50);

        health.SetCurrent(-10);

        health.Current.Value.Should().Be(0);
    }

    [Fact]
    public void SetCurrent_WithinRange_SetsExactValue()
    {
        var (health, _) = Create();
        health.SetMax(100);

        health.SetCurrent(42);

        health.Current.Value.Should().Be(42);
    }

    [Fact]
    public void TakeDamage_ReducesCurrentHp()
    {
        var (health, _) = Create();
        health.SetMax(100);
        health.SetCurrent(100);

        health.TakeDamage(30);

        health.Current.Value.Should().Be(70);
    }

    [Fact]
    public void TakeDamage_BelowZero_ClampsToZero()
    {
        var (health, _) = Create();
        health.SetMax(100);
        health.SetCurrent(10);

        health.TakeDamage(50);

        health.Current.Value.Should().Be(0);
    }

    [Fact]
    public void TakeDamage_NegativeAmount_Throws()
    {
        var (health, _) = Create();
        health.SetMax(100);
        health.SetCurrent(50);

        var act = () => health.TakeDamage(-5);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Heal_IncreasesCurrentHp()
    {
        var (health, _) = Create();
        health.SetMax(100);
        health.SetCurrent(50);

        health.Heal(20);

        health.Current.Value.Should().Be(70);
    }

    [Fact]
    public void Heal_AboveMax_ClampsToMax()
    {
        var (health, _) = Create();
        health.SetMax(100);
        health.SetCurrent(90);

        health.Heal(50);

        health.Current.Value.Should().Be(100);
    }

    [Fact]
    public void Heal_NegativeAmount_Throws()
    {
        var (health, _) = Create();

        var act = () => health.Heal(-1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TakeDamage_ToZero_CurrentIsZero()
    {
        var (health, _) = Create();
        health.SetMax(50);
        health.SetCurrent(50);

        health.TakeDamage(50);

        health.Current.Value.Should().Be(0);
    }

    [Fact]
    public void SyncState_ReflectsInValueProperty()
    {
        var (health, state) = Create();
        health.SetMax(80);
        health.SetCurrent(45);

        state.Value.Max.Should().Be(80);
        state.Value.Current.Should().Be(45);
    }
}

public class ManaTests
{
    private static (Mana mana, ValueProperty<PlayerManaState> state) Create()
    {
        var state = new ValueProperty<PlayerManaState>(0).ForTest();
        var mana = new Mana(state, Substitute.For<IModifiers>());
        return (mana, state);
    }

    [Fact]
    public void SetMax_SetsMaximumMana()
    {
        var (mana, _) = Create();

        mana.SetMax(10);

        mana.Max.Should().Be(10);
    }

    [Fact]
    public void SetMax_ClampsCurrent_WhenCurrentExceedsNewMax()
    {
        var (mana, _) = Create();
        mana.SetMax(10);
        mana.Restore();
        mana.Current.Should().Be(10);

        mana.SetMax(5);

        mana.Current.Should().Be(5);
    }

    [Fact]
    public void Restore_SetsCurrentToMax()
    {
        var (mana, _) = Create();
        mana.SetMax(10);
        mana.Use(5);

        mana.Restore();

        mana.Current.Should().Be(10);
    }

    [Fact]
    public void Use_ReducesCurrentMana()
    {
        var (mana, _) = Create();
        mana.SetMax(10);
        mana.Restore();

        mana.Use(3);

        mana.Current.Should().Be(7);
    }

    [Fact]
    public void Use_MoreThanAvailable_ClampsToZero()
    {
        var (mana, _) = Create();
        mana.SetMax(5);
        mana.Restore();

        mana.Use(20);

        mana.Current.Should().Be(0);
    }

    [Fact]
    public void Use_NegativeAmount_Throws()
    {
        var (mana, _) = Create();

        var act = () => mana.Use(-1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCurrent_ClampsToMax()
    {
        var (mana, _) = Create();
        mana.SetMax(10);

        mana.SetCurrent(50);

        mana.Current.Should().Be(10);
    }

    [Fact]
    public void SetCurrent_ClampsToZero()
    {
        var (mana, _) = Create();
        mana.SetMax(10);

        mana.SetCurrent(-5);

        mana.Current.Should().Be(0);
    }

    [Fact]
    public void SyncState_ReflectsInValueProperty()
    {
        var (mana, state) = Create();
        mana.SetMax(8);
        mana.Restore();
        mana.Use(3);

        state.Value.Max.Should().Be(8);
        state.Value.Current.Should().Be(5);
    }
}

public class MovesTests
{
    private static (Moves moves, ValueProperty<PlayerMovesState> state) Create()
    {
        var state = new ValueProperty<PlayerMovesState>(0).ForTest();
        var modifiers = new Modifiers(new ValueProperty<PlayerModifiersState>(0).ForTest());
        var moves = new Moves(state, modifiers);
        return (moves, state);
    }

    [Fact]
    public void SetMax_SetsMaxMoves()
    {
        var (moves, _) = Create();

        moves.SetMax(5);

        moves.Max.Should().Be(5);
    }

    [Fact]
    public void Restore_ResetsLeftToMax()
    {
        var (moves, _) = Create();
        moves.SetMax(3);

        moves.Restore();

        moves.Left.Should().Be(3);
    }

    [Fact]
    public void OnUsed_DecrementsByOne()
    {
        var (moves, _) = Create();
        moves.SetMax(3);
        moves.Restore();

        moves.OnUsed();

        moves.Left.Should().Be(2);
    }

    [Fact]
    public void OnUsed_AtZero_Throws()
    {
        var (moves, _) = Create();
        moves.SetMax(1);
        moves.Restore();
        moves.OnUsed();

        var act = () => moves.OnUsed();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Lock_SetsLeftToZero()
    {
        var (moves, state) = Create();
        moves.SetMax(5);
        moves.Restore();

        moves.Lock();

        moves.Left.Should().Be(0);
        state.Value.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Restore_SetsIsAvailableTrue()
    {
        var (moves, state) = Create();
        moves.SetMax(3);
        moves.Lock();

        moves.Restore();

        state.Value.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void SetCurrent_ClampsToMax()
    {
        var (moves, _) = Create();
        moves.SetMax(5);

        moves.SetCurrent(100);

        moves.Left.Should().Be(5);
    }

    [Fact]
    public void SetCurrent_ClampsToZero()
    {
        var (moves, _) = Create();
        moves.SetMax(5);

        moves.SetCurrent(-10);

        moves.Left.Should().Be(0);
    }

    [Fact]
    public void SetMax_ClampsLeftWhenExceeds()
    {
        var (moves, _) = Create();
        moves.SetMax(10);
        moves.Restore();

        moves.SetMax(3);

        moves.Left.Should().Be(3);
    }

    [Fact]
    public void SyncState_ReflectsMaxInState()
    {
        var (moves, state) = Create();
        moves.SetMax(7);
        moves.Restore();

        state.Value.Max.Should().Be(7);
        state.Value.Left.Should().Be(7);
    }
}

public class DeckTests
{
    private static (Deck deck, ValueProperty<PlayerDeckState> state) Create(IReadOnlyList<CardType>? selected = null)
    {
        var state = new ValueProperty<PlayerDeckState>(0).ForTest();
        state.Update(s => s.Queue = new List<CardType>());
        selected ??= new[] { CardType.Bloodhound, CardType.Trebuchet };
        var deck = new Deck(state, selected);
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
        var hand = new Hand(state);
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
        var stash = new Stash(state);
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

    [Fact]
    public void SyncState_CountReflectsInState()
    {
        var (stash, state) = Create();
        stash.Add(CardType.Bloodhound);
        stash.Add(CardType.Trebuchet);

        state.Value.Count.Should().Be(2);

        stash.Pick();
        state.Value.Count.Should().Be(1);
    }
}

public class RoundActionServiceTests
{
    private class TestAction : IRoundAction
    {
        public int ExecuteCount { get; private set; }

        public void Execute()
        {
            ExecuteCount++;
        }
    }

    [Fact]
    public void Schedule_WithDelay_ExecutesAfterNTicks()
    {
        var service = new RoundActionService();
        var action = new TestAction();

        service.Schedule(action, 3);

        service.Tick();
        action.ExecuteCount.Should().Be(0);
        service.Tick();
        action.ExecuteCount.Should().Be(0);
        service.Tick();
        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Schedule_WithOneRound_ExecutesOnFirstTick()
    {
        var service = new RoundActionService();
        var action = new TestAction();

        service.Schedule(action, 1);
        service.Tick();

        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Schedule_WithZeroRounds_DoesNotSchedule()
    {
        var service = new RoundActionService();
        var action = new TestAction();

        service.Schedule(action, 0);
        service.Tick();

        action.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public void Schedule_NegativeRounds_DoesNotSchedule()
    {
        var service = new RoundActionService();
        var action = new TestAction();

        service.Schedule(action, -5);
        service.Tick();

        action.ExecuteCount.Should().Be(0);
    }

    [Fact]
    public void Tick_WithNoActions_DoesNotThrow()
    {
        var service = new RoundActionService();

        var act = () => service.Tick();

        act.Should().NotThrow();

        // Verify idempotency — ticking twice with no actions is also safe
        service.Tick();
        service.Tick();

        // Schedule an action after empty ticks to confirm service is still functional
        var action = new TestAction();
        service.Schedule(action, 1);
        service.Tick();
        action.ExecuteCount.Should().Be(1, "service should still work after empty ticks");
    }

    [Fact]
    public void Tick_MultipleActionsAtSameRound_AllExecute()
    {
        var service = new RoundActionService();
        var action1 = new TestAction();
        var action2 = new TestAction();

        service.Schedule(action1, 2);
        service.Schedule(action2, 2);

        service.Tick();
        action1.ExecuteCount.Should().Be(0);
        action2.ExecuteCount.Should().Be(0);

        service.Tick();
        action1.ExecuteCount.Should().Be(1);
        action2.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void Action_RemovedAfterExecution()
    {
        var service = new RoundActionService();
        var action = new TestAction();

        service.Schedule(action, 1);
        service.Tick();
        service.Tick();

        action.ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void MultipleActions_DifferentDelays_ExecuteAtCorrectTimes()
    {
        var service = new RoundActionService();
        var early = new TestAction();
        var late = new TestAction();

        service.Schedule(early, 1);
        service.Schedule(late, 3);

        service.Tick();
        early.ExecuteCount.Should().Be(1);
        late.ExecuteCount.Should().Be(0);

        service.Tick();
        late.ExecuteCount.Should().Be(0);

        service.Tick();
        late.ExecuteCount.Should().Be(1);
    }
}

public class ModifiersTests
{
    private static (Modifiers modifiers, ValueProperty<PlayerModifiersState> state) Create()
    {
        var state = new ValueProperty<PlayerModifiersState>(0).ForTest();
        var modifiers = new Modifiers(state);
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

        modifiers.Set(PlayerModifier.TrebuchetBoost, 5f);

        modifiers.Values[PlayerModifier.TrebuchetBoost].Should().Be(5f);
    }

    [Fact]
    public void Get_ReturnsCurrentValue()
    {
        var (modifiers, _) = Create();
        modifiers.Set(PlayerModifier.TrebuchetBoost, 3.5f);

        var value = modifiers.Get(PlayerModifier.TrebuchetBoost);

        value.Should().Be(3.5f);
    }

    [Fact]
    public void Inc_IncrementsByOne()
    {
        var (modifiers, _) = Create();

        modifiers.Inc(PlayerModifier.TrebuchetBoost);
        modifiers.Inc(PlayerModifier.TrebuchetBoost);

        modifiers.Values[PlayerModifier.TrebuchetBoost].Should().Be(2f);
    }

    [Fact]
    public void Reset_SetsToZero()
    {
        var (modifiers, _) = Create();
        modifiers.Set(PlayerModifier.TrebuchetBoost, 10f);

        modifiers.Reset(PlayerModifier.TrebuchetBoost);

        modifiers.Values[PlayerModifier.TrebuchetBoost].Should().Be(0f);
    }

    [Fact]
    public void SyncState_ReflectsInValueProperty()
    {
        var (modifiers, state) = Create();

        modifiers.Set(PlayerModifier.TrebuchetBoost, 7f);

        state.Value.Values[PlayerModifier.TrebuchetBoost].Should().Be(7f);
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
        actions.OnCardUsed();

        fired.Should().BeTrue();
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
        actions.OnCardUsed();

        fired.Should().BeFalse();
    }
}