using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class PlayerCardTestsBase
{
    protected static IPlayer MockPlayer(Guid? id = null)
    {
        var player = Substitute.For<IPlayer>();
        var user = Substitute.For<IUser>();
        user.Id.Returns(id ?? Guid.NewGuid());
        player.User.Returns(user);
        player.Health.Returns(Substitute.For<IHealth>());
        player.Mana.Returns(Substitute.For<IMana>());
        player.Moves.Returns(Substitute.For<IMoves>());
        player.Hand.Returns(Substitute.For<IHand>());
        player.Deck.Returns(Substitute.For<IDeck>());
        player.Stash.Returns(Substitute.For<IStash>());
        player.Modifiers.Returns(Substitute.For<IModifiers>());
        player.Board.Returns(Substitute.For<IBoard>());
        return player;
    }

    protected static MoveSnapshot CreateSnapshot() => new();
}

public class TrebuchetAimerTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_NoExistingModifier_AddsTrebuchetBoost()
    {
        var owner = MockPlayer();
        var config = new CardConfigOptions.TrebuchetAimer { Size = 1 };

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 0f }
        });

        var result = new TrebuchetAimer(owner, config).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Set(PlayerModifier.TrebuchetBoost, 1f);
    }

    [Fact]
    public void Use_ExistingModifier_StacksWithCurrentValue()
    {
        var owner = MockPlayer();
        var config = new CardConfigOptions.TrebuchetAimer { Size = 1 };

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 2f }
        });

        var result = new TrebuchetAimer(owner, config).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Set(PlayerModifier.TrebuchetBoost, 3f);
    }

    [Fact]
    public void Use_LargeSize_AddsFullSize()
    {
        var owner = MockPlayer();
        var config = new CardConfigOptions.TrebuchetAimer { Size = 5 };

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 0f }
        });

        var result = new TrebuchetAimer(owner, config).Use();

        owner.Modifiers.Received(1).Set(PlayerModifier.TrebuchetBoost, 5f);
    }

    [Fact]
    public void Use_ReturnsActionData()
    {
        var owner = MockPlayer();
        var config = new CardConfigOptions.TrebuchetAimer { Size = 1 };

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 0f }
        });

        var result = new TrebuchetAimer(owner, config).Use();

        result.ActionData.Should().BeOfType<CardActionSnapshot.TrebuchetAimer>();
    }
}

public class MedicTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_HealsOneHp()
    {
        var owner = MockPlayer();

        var result = new Medic(owner).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Health.Received(1).Heal(1);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);

        var result = new Medic(owner).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Medic>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}

public class SiphonTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_DrainsFromOpponentManaMax()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Siphon { DrainAmount = 1 };
        opponent.Mana.Max.Returns(5);
        owner.Mana.Max.Returns(3);

        var result = new Siphon(owner, opponent, config).Use();

        result.Result.HasError.Should().BeFalse();
        opponent.Mana.Received(1).SetMax(4);
    }

    [Fact]
    public void Use_AddsDrainedManaToOwnerMax()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Siphon { DrainAmount = 1 };
        opponent.Mana.Max.Returns(5);
        owner.Mana.Max.Returns(3);

        new Siphon(owner, opponent, config).Use();

        owner.Mana.Received(1).SetMax(4);
    }

    [Fact]
    public void Use_OpponentHasZeroMana_StillAdjusts()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Siphon { DrainAmount = 2 };
        opponent.Mana.Max.Returns(0);
        owner.Mana.Max.Returns(3);

        var result = new Siphon(owner, opponent, config).Use();

        result.Result.HasError.Should().BeFalse();
        opponent.Mana.Received(1).SetMax(-2);
        owner.Mana.Received(1).SetMax(5);
    }

    [Fact]
    public void Use_ActionDataReferencesOpponent()
    {
        var opponentId = Guid.NewGuid();
        var owner = MockPlayer();
        var opponent = MockPlayer(opponentId);
        var config = new CardConfigOptions.Siphon { DrainAmount = 1 };
        opponent.Mana.Max.Returns(5);
        owner.Mana.Max.Returns(3);

        var result = new Siphon(owner, opponent, config).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Siphon>().Subject;
        actionData.TargetPlayer.Should().Be(opponentId);
    }

    [Fact]
    public void Use_LargeDrainAmount_DrainsFullAmount()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Siphon { DrainAmount = 5 };
        opponent.Mana.Max.Returns(10);
        owner.Mana.Max.Returns(3);

        new Siphon(owner, opponent, config).Use();

        opponent.Mana.Received(1).SetMax(5);
        owner.Mana.Received(1).SetMax(8);
    }
}

public class OverclockTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_AddsExtraMovesToCurrent()
    {
        var owner = MockPlayer();
        var config = new CardConfigOptions.Overclock();
        owner.Moves.Left.Returns(3);

        var result = new Overclock(owner, config).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Moves.Received(1).SetCurrent(3 + config.ExtraMoves);
    }

    [Fact]
    public void Use_ZeroMovesLeft_StillAddsExtraMoves()
    {
        var owner = MockPlayer();
        var config = new CardConfigOptions.Overclock();
        owner.Moves.Left.Returns(0);

        new Overclock(owner, config).Use();

        owner.Moves.Received(1).SetCurrent(config.ExtraMoves);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var config = new CardConfigOptions.Overclock();
        owner.Moves.Left.Returns(3);

        var result = new Overclock(owner, config).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Overclock>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}

public class GraveDiggerTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_MovesCardFromStashToHand()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var snapshot = CreateSnapshot();
        owner.Stash.Count.Returns(1);
        owner.Stash.Pick().Returns(CardType.Bloodhound);
        var activeCard = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound };
        owner.Hand.Add(CardType.Bloodhound).Returns(activeCard);

        var result = new GraveDigger(owner, snapshot).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Stash.Received(1).Pick();
        owner.Hand.Received(1).Add(CardType.Bloodhound);
    }

    [Fact]
    public void Use_EmptyStash_Fails()
    {
        var owner = MockPlayer();
        var snapshot = CreateSnapshot();
        owner.Stash.Count.Returns(0);

        var result = new GraveDigger(owner, snapshot).Use();

        result.Result.HasError.Should().BeTrue();
        result.ActionData.Should().BeNull();
    }

    [Fact]
    public void Use_SnapshotRecordsCardAdd()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var snapshot = CreateSnapshot();
        owner.Stash.Count.Returns(1);
        owner.Stash.Pick().Returns(CardType.Trebuchet);
        var cardId = Guid.NewGuid();
        var activeCard = new ActiveCard { Id = cardId, Type = CardType.Trebuchet };
        owner.Hand.Add(CardType.Trebuchet).Returns(activeCard);

        new GraveDigger(owner, snapshot).Use();

        var collected = snapshot.Collect();
        collected.Records.Should().ContainSingle();
        var record = collected.Records[0].Should().BeOfType<PlayerSnapshotRecord.CardAdd>().Subject;
        record.PlayerId.Should().Be(ownerId);
        record.CardId.Should().Be(cardId);
        record.Type.Should().Be(CardType.Trebuchet);
    }

    [Fact]
    public void Use_ReturnsGravediggerActionData()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var snapshot = CreateSnapshot();
        owner.Stash.Count.Returns(1);
        owner.Stash.Pick().Returns(CardType.Bloodhound);
        owner.Hand.Add(CardType.Bloodhound).Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound });

        var result = new GraveDigger(owner, snapshot).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Gravedigger>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}

public class ScavengerTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_DrawsDrawCountCardsFromDeck()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var snapshot = CreateSnapshot();
        var config = new CardConfigOptions.Scavenger();
        owner.Deck.Count.Returns(5, 4);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound, CardType.Trebuchet);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound },
                 new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Trebuchet });

        var result = new Scavenger(owner, snapshot, config).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Deck.Received(config.DrawCount).DrawCard();
        owner.Hand.Received(config.DrawCount).Add(Arg.Any<CardType>());
    }

    [Fact]
    public void Use_DeckHasFewerCards_DrawsWhatAvailable()
    {
        var owner = MockPlayer();
        var snapshot = CreateSnapshot();
        var config = new CardConfigOptions.Scavenger();
        // Deck has 1 card, config wants 2
        owner.Deck.Count.Returns(1, 0);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound });

        var result = new Scavenger(owner, snapshot, config).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Deck.Received(1).DrawCard();
    }

    [Fact]
    public void Use_EmptyDeck_DrawsNothing()
    {
        var owner = MockPlayer();
        var snapshot = CreateSnapshot();
        var config = new CardConfigOptions.Scavenger();
        owner.Deck.Count.Returns(0);

        var result = new Scavenger(owner, snapshot, config).Use();

        result.Result.HasError.Should().BeFalse();
        owner.Deck.DidNotReceive().DrawCard();
    }

    [Fact]
    public void Use_SnapshotRecordsCardAdditions()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var snapshot = CreateSnapshot();
        var config = new CardConfigOptions.Scavenger();
        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();
        owner.Deck.Count.Returns(5, 4);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound, CardType.Trebuchet);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = cardId1, Type = CardType.Bloodhound },
                 new ActiveCard { Id = cardId2, Type = CardType.Trebuchet });

        new Scavenger(owner, snapshot, config).Use();

        var collected = snapshot.Collect();
        collected.Records.Should().HaveCount(config.DrawCount);
        collected.Records[0].Should().BeOfType<PlayerSnapshotRecord.CardAdd>();
        collected.Records[1].Should().BeOfType<PlayerSnapshotRecord.CardAdd>();
    }

    [Fact]
    public void Use_ReturnsScavengerActionData()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var snapshot = CreateSnapshot();
        var config = new CardConfigOptions.Scavenger();
        owner.Deck.Count.Returns(5, 4);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound, CardType.Trebuchet);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound },
                 new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Trebuchet });

        var result = new Scavenger(owner, snapshot, config).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Scavenger>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}

public class HandScrambleTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_RemovesOpponentHandCardsAndDrawsNew()
    {
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var snapshot = CreateSnapshot();

        var card1 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound };
        var card2 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Trebuchet };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1, card2 });
        opponent.Deck.Count.Returns(2, 1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke, CardType.Medic);

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Smoke },
                    new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Medic });

        var result = new HandScramble(opponent, snapshot).Use();

        result.Result.HasError.Should().BeFalse();
        opponent.Hand.Received(1).Remove(card1.Id);
        opponent.Hand.Received(1).Remove(card2.Id);
        opponent.Deck.Received(1).Shuffle();
        opponent.Deck.Received(2).DrawCard();
        opponent.Hand.Received(2).Add(Arg.Any<CardType>());
    }

    [Fact]
    public void Use_EmptyHand_Fails()
    {
        var opponent = MockPlayer();
        var snapshot = CreateSnapshot();
        opponent.Hand.Entries.Returns(new List<ActiveCard>());

        var result = new HandScramble(opponent, snapshot).Use();

        result.Result.HasError.Should().BeTrue();
        result.ActionData.Should().BeNull();
    }

    [Fact]
    public void Use_SnapshotRecordsRemovalsAndAdditions()
    {
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var snapshot = CreateSnapshot();

        var cardId1 = Guid.NewGuid();
        var cardId2 = Guid.NewGuid();
        var card1 = new ActiveCard { Id = cardId1, Type = CardType.Bloodhound };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1 });
        opponent.Deck.Count.Returns(1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke);
        var newCardId = Guid.NewGuid();

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = newCardId, Type = CardType.Smoke });

        new HandScramble(opponent, snapshot).Use();

        var collected = snapshot.Collect();
        collected.Records.Should().HaveCount(2);
        var removeRecord = collected.Records[0].Should().BeOfType<PlayerSnapshotRecord.CardRemove>().Subject;
        removeRecord.PlayerId.Should().Be(opponentId);
        removeRecord.CardId.Should().Be(cardId1);
        var addRecord = collected.Records[1].Should().BeOfType<PlayerSnapshotRecord.CardAdd>().Subject;
        addRecord.PlayerId.Should().Be(opponentId);
        addRecord.CardId.Should().Be(newCardId);
    }

    [Fact]
    public void Use_OldCardsReturnedToDeck()
    {
        var opponent = MockPlayer();
        var snapshot = CreateSnapshot();

        var card1 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1 });
        opponent.Deck.Count.Returns(1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke);

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Smoke });

        new HandScramble(opponent, snapshot).Use();

        opponent.Deck.Received(1).AddCard(CardType.Bloodhound);
    }

    [Fact]
    public void Use_ActionDataReferencesOpponent()
    {
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var snapshot = CreateSnapshot();

        var card1 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1 });
        opponent.Deck.Count.Returns(1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke);

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Smoke });

        var result = new HandScramble(opponent, snapshot).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.HandScramble>().Subject;
        actionData.TargetPlayer.Should().Be(opponentId);
    }
}

public class LockdownTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_ReducesOpponentMaxMoves()
    {
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Lockdown { MovesReduction = 1, Duration = 2 };
        var roundActionService = Substitute.For<IRoundActionService>();
        opponent.Moves.Max.Returns(3);

        var result = new Lockdown(opponent, config, roundActionService).Use();

        result.Result.HasError.Should().BeFalse();
        opponent.Moves.Received(1).SetMax(2);
    }

    [Fact]
    public void Use_LargeReduction_ClampsToZero()
    {
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Lockdown { MovesReduction = 10, Duration = 2 };
        var roundActionService = Substitute.For<IRoundActionService>();
        opponent.Moves.Max.Returns(3);

        new Lockdown(opponent, config, roundActionService).Use();

        opponent.Moves.Received(1).SetMax(0);
    }

    [Fact]
    public void Use_SchedulesRestoration()
    {
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Lockdown { MovesReduction = 1, Duration = 2 };
        var roundActionService = Substitute.For<IRoundActionService>();
        opponent.Moves.Max.Returns(3);

        new Lockdown(opponent, config, roundActionService).Use();

        roundActionService.Received(1)
                          .Schedule(Arg.Any<LockdownDisposeAction>(), config.Duration);
    }

    [Fact]
    public void Use_DisposeActionRestoresOriginalMax()
    {
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Lockdown { MovesReduction = 1, Duration = 2 };
        var roundActionService = new RoundActionService();
        opponent.Moves.Max.Returns(5);

        new Lockdown(opponent, config, roundActionService).Use();

        // Tick twice to trigger the scheduled action (duration = 2)
        roundActionService.Tick();
        roundActionService.Tick();

        // Received SetMax(4) from Lockdown, then SetMax(5) from dispose action
        opponent.Moves.Received(1).SetMax(4);
        opponent.Moves.Received(1).SetMax(5);
    }

    [Fact]
    public void Use_ActionDataReferencesOpponent()
    {
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var config = new CardConfigOptions.Lockdown { MovesReduction = 1, Duration = 2 };
        var roundActionService = Substitute.For<IRoundActionService>();
        opponent.Moves.Max.Returns(3);

        var result = new Lockdown(opponent, config, roundActionService).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.Lockdown>().Subject;
        actionData.TargetPlayer.Should().Be(opponentId);
    }

    [Fact]
    public void Use_DisposeActionDoesNotFireBeforeDuration()
    {
        var opponent = MockPlayer();
        var config = new CardConfigOptions.Lockdown { MovesReduction = 1, Duration = 3 };
        var roundActionService = new RoundActionService();
        opponent.Moves.Max.Returns(5);

        new Lockdown(opponent, config, roundActionService).Use();

        // Only tick once — action should not fire yet
        roundActionService.Tick();

        // SetMax(4) from Lockdown, but no SetMax(5) yet
        opponent.Moves.Received(1).SetMax(4);
        opponent.Moves.DidNotReceive().SetMax(5);
    }
}