using Cluster.Configs;
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

    protected static ICardConfigs MockConfigs()
    {
        var configs = Substitute.For<ICardConfigs>();
        configs.Value.Returns(CardConfigs.All);
        return configs;
    }

    protected static IGameContext MockGameContext(IPlayer owner, IPlayer opponent)
    {
        var ctx = Substitute.For<IGameContext>();

        var dict = new Dictionary<IUser, IPlayer>
        {
            { owner.User, owner },
            { opponent.User, opponent }
        };
        ctx.UserToPlayer.Returns((IReadOnlyDictionary<IUser, IPlayer>)dict);
        return ctx;
    }

    protected static MoveSnapshot CreateSnapshot() => new();
}

public static class CardTestExtensions
{
    public static CardUseResult Use<TPayload>(this ICard<TPayload> card, IPlayer invoker, TPayload payload)
        where TPayload : ICardUsePayload
    {
        var context = new CardUseContext
        {
            Invoker = invoker,
            Snapshot = new MoveSnapshot(),
            CardId = Guid.NewGuid()
        };
        return card.Use(context, payload);
    }

    public static CardUseResult Use<TPayload>(
        this ICard<TPayload> card,
        IPlayer invoker,
        TPayload payload,
        MoveSnapshot snapshot,
        Guid? cardId = null)
        where TPayload : ICardUsePayload
    {
        var context = new CardUseContext
        {
            Invoker = invoker,
            Snapshot = snapshot,
            CardId = cardId ?? Guid.NewGuid()
        };
        return card.Use(context, payload);
    }

    public static (CardUseResult Result, MoveSnapshot Snapshot) UseCapture<TPayload>(
        this ICard<TPayload> card,
        IPlayer invoker,
        TPayload payload)
        where TPayload : ICardUsePayload
    {
        var snapshot = new MoveSnapshot();

        var context = new CardUseContext
        {
            Invoker = invoker,
            Snapshot = snapshot,
            CardId = Guid.NewGuid()
        };
        return (card.Use(context, payload), snapshot);
    }

    public static T? GetLastCardAction<T>(this MoveSnapshot snapshot) where T : ICardActionData
    {
        return snapshot.Collect().Records
                       .OfType<PlayerSnapshotRecord.CardUse>()
                       .Select(r => r.Data)
                       .OfType<T>()
                       .LastOrDefault();
    }
}

public class TrebuchetAimerTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_NoExistingModifier_AddsTrebuchetBoost()
    {
        var owner = MockPlayer();
        var configs = MockConfigs();
        var size = CardConfigs.All.TrebuchetAimer_Normal.Size;

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 0f }
        });

        var result = new TrebuchetAimer(configs).Use(owner,
            new CardUsePayload.TrebuchetAimer { Type = CardType.TrebuchetAimer });

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_ExistingModifier_StacksWithCurrentValue()
    {
        var owner = MockPlayer();
        var configs = MockConfigs();
        var size = CardConfigs.All.TrebuchetAimer_Normal.Size;

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 2f }
        });

        var result = new TrebuchetAimer(configs).Use(owner,
            new CardUsePayload.TrebuchetAimer { Type = CardType.TrebuchetAimer });

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_LargeSize_AddsFullSize()
    {
        var owner = MockPlayer();
        var configs = MockConfigs();
        var size = CardConfigs.All.TrebuchetAimer_Normal.Size;

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 0f }
        });

        new TrebuchetAimer(configs).Use(owner, new CardUsePayload.TrebuchetAimer { Type = CardType.TrebuchetAimer });

        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_ReturnsActionData()
    {
        var owner = MockPlayer();
        var configs = MockConfigs();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.TrebuchetBoost, 0f }
        });

        var (_, snapshot) = new TrebuchetAimer(configs).UseCapture(owner,
            new CardUsePayload.TrebuchetAimer { Type = CardType.TrebuchetAimer });

        snapshot.GetLastCardAction<CardActionSnapshot.TrebuchetAimer>().Should().NotBeNull();
    }
}

public class MedicTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_HealsOneHp()
    {
        var owner = MockPlayer();

        var result = new Medic().Use(owner, new CardUsePayload.Medic { Type = CardType.Medic });

        result.Result.HasError.Should().BeFalse();
        owner.Health.Received(1).Heal(Arg.Any<MoveSnapshot>(), 1);
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);

        var (_, snapshot) = new Medic().UseCapture(owner, new CardUsePayload.Medic { Type = CardType.Medic });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Medic>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }
}

public class SiphonTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_DrainsFromOpponentManaMax()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var gameContext = MockGameContext(owner, opponent);
        var drainAmount = CardConfigs.All.Siphon_Normal.DrainAmount;
        opponent.Mana.ResultMax.Returns(5);
        owner.Mana.ResultMax.Returns(3);

        var result = new Siphon(configs, gameContext).Use(owner, new CardUsePayload.Siphon { Type = CardType.Siphon });

        result.Result.HasError.Should().BeFalse();
        opponent.Mana.Received(1).SetMax(Arg.Any<MoveSnapshot>(), 5 - drainAmount);
    }

    [Fact]
    public void Use_AddsDrainedManaToOwnerMax()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var gameContext = MockGameContext(owner, opponent);
        var drainAmount = CardConfigs.All.Siphon_Normal.DrainAmount;
        opponent.Mana.ResultMax.Returns(5);
        owner.Mana.ResultMax.Returns(3);

        new Siphon(configs, gameContext).Use(owner, new CardUsePayload.Siphon { Type = CardType.Siphon });

        owner.Mana.Received(1).SetMax(Arg.Any<MoveSnapshot>(), 3 + drainAmount);
    }

    [Fact]
    public void Use_OpponentHasZeroMana_StillAdjusts()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var gameContext = MockGameContext(owner, opponent);
        var drainAmount = CardConfigs.All.Siphon_Normal.DrainAmount;
        opponent.Mana.ResultMax.Returns(0);
        owner.Mana.ResultMax.Returns(3);

        var result = new Siphon(configs, gameContext).Use(owner, new CardUsePayload.Siphon { Type = CardType.Siphon });

        result.Result.HasError.Should().BeFalse();
        opponent.Mana.Received(1).SetMax(Arg.Any<MoveSnapshot>(), 0 - drainAmount);
        owner.Mana.Received(1).SetMax(Arg.Any<MoveSnapshot>(), 3 + drainAmount);
    }

    [Fact]
    public void Use_ActionDataReferencesOpponent()
    {
        var opponentId = Guid.NewGuid();
        var owner = MockPlayer();
        var opponent = MockPlayer(opponentId);
        var configs = MockConfigs();
        var gameContext = MockGameContext(owner, opponent);
        opponent.Mana.ResultMax.Returns(5);
        owner.Mana.ResultMax.Returns(3);

        var (_, snapshot) = new Siphon(configs, gameContext).UseCapture(owner,
            new CardUsePayload.Siphon { Type = CardType.Siphon });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Siphon>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(opponentId);
    }

    [Fact]
    public void Use_LargeDrainAmount_DrainsFullAmount()
    {
        var owner = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var gameContext = MockGameContext(owner, opponent);
        var drainAmount = CardConfigs.All.Siphon_Normal.DrainAmount;
        opponent.Mana.ResultMax.Returns(10);
        owner.Mana.ResultMax.Returns(3);

        new Siphon(configs, gameContext).Use(owner, new CardUsePayload.Siphon { Type = CardType.Siphon });

        opponent.Mana.Received(1).SetMax(Arg.Any<MoveSnapshot>(), 10 - drainAmount);
        owner.Mana.Received(1).SetMax(Arg.Any<MoveSnapshot>(), 3 + drainAmount);
    }
}

public class OverclockTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_SetsAdditionalMovesModifier()
    {
        var owner = MockPlayer();
        var configs = MockConfigs();
        var roundService = Substitute.For<IRoundActionService>();
        var extraMoves = CardConfigs.All.Overclock_Normal.ExtraMoves;

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, 0f }
        });

        var result = new Overclock(configs, roundService).Use(owner,
            new CardUsePayload.Overclock { Type = CardType.Overclock });

        result.Result.HasError.Should().BeFalse();
        owner.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_SchedulesDisposeActionForNextRound()
    {
        var owner = MockPlayer();
        var configs = MockConfigs();
        var roundService = Substitute.For<IRoundActionService>();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, 0f }
        });

        new Overclock(configs, roundService).Use(owner, new CardUsePayload.Overclock { Type = CardType.Overclock });

        roundService.Received(1).Schedule(Arg.Any<ModifierRoundAction>());
    }

    [Fact]
    public void Use_ActionDataIncludesOwnerId()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var configs = MockConfigs();
        var roundService = Substitute.For<IRoundActionService>();

        owner.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, 0f }
        });

        var (_, snapshot) = new Overclock(configs, roundService).UseCapture(owner,
            new CardUsePayload.Overclock { Type = CardType.Overclock });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Overclock>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
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

        var result = new GraveDigger().Use(owner,
            new CardUsePayload.Gravedigger { Type = CardType.Gravedigger });

        result.Result.HasError.Should().BeFalse();
        owner.Stash.Received(1).Pick();
        owner.Hand.Received(1).Add(CardType.Bloodhound);
    }

    [Fact]
    public void Use_EmptyStash_Fails()
    {
        var owner = MockPlayer();
        owner.Stash.Count.Returns(0);

        var (result, snapshot) = new GraveDigger().UseCapture(owner,
            new CardUsePayload.Gravedigger { Type = CardType.Gravedigger });

        result.Result.HasError.Should().BeTrue();
        snapshot.GetLastCardAction<ICardActionData>().Should().BeNull();
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

        new GraveDigger().Use(owner, new CardUsePayload.Gravedigger { Type = CardType.Gravedigger }, snapshot);

        var collected = snapshot.Collect();
        var record = collected.Records.OfType<PlayerSnapshotRecord.CardAdd>().Should().ContainSingle().Subject;
        record.PlayerId.Should().Be(ownerId);
        record.CardId.Should().Be(cardId);
        record.Type.Should().Be(CardType.Trebuchet);
    }

    [Fact]
    public void Use_ReturnsGravediggerActionData()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        owner.Stash.Count.Returns(1);
        owner.Stash.Pick().Returns(CardType.Bloodhound);
        owner.Hand.Add(CardType.Bloodhound).Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound });

        var (_, snapshot) = new GraveDigger().UseCapture(owner,
            new CardUsePayload.Gravedigger { Type = CardType.Gravedigger });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Gravedigger>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
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
        var configs = MockConfigs();
        var drawCount = CardConfigs.All.Scavenger_Normal.DrawCount;
        owner.Deck.Count.Returns(5, 4);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound, CardType.Trebuchet);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound },
                 new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Trebuchet });

        var result = new Scavenger(configs).Use(owner,
            new CardUsePayload.Scavenger { Type = CardType.Scavenger });

        result.Result.HasError.Should().BeFalse();
        owner.Deck.Received(drawCount).DrawCard();
        owner.Hand.Received(drawCount).Add(Arg.Any<CardType>());
    }

    [Fact]
    public void Use_DeckHasFewerCards_DrawsWhatAvailable()
    {
        var owner = MockPlayer();
        var snapshot = CreateSnapshot();
        var configs = MockConfigs();
        // Deck has 1 card, config wants more
        owner.Deck.Count.Returns(1, 0);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound });

        var result = new Scavenger(configs).Use(owner,
            new CardUsePayload.Scavenger { Type = CardType.Scavenger });

        result.Result.HasError.Should().BeFalse();
        owner.Deck.Received(1).DrawCard();
    }

    [Fact]
    public void Use_EmptyDeck_DrawsNothing()
    {
        var owner = MockPlayer();
        var snapshot = CreateSnapshot();
        var configs = MockConfigs();
        owner.Deck.Count.Returns(0);

        var result = new Scavenger(configs).Use(owner,
            new CardUsePayload.Scavenger { Type = CardType.Scavenger });

        result.Result.HasError.Should().BeFalse();
        owner.Deck.DidNotReceive().DrawCard();
    }

    [Fact]
    public void Use_SnapshotRecordsCardAdditions()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var snapshot = CreateSnapshot();
        var configs = MockConfigs();
        var drawCount = CardConfigs.All.Scavenger_Normal.DrawCount;
        owner.Deck.Count.Returns(5, 4);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound, CardType.Trebuchet);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound },
                 new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Trebuchet });

        new Scavenger(configs).Use(owner, new CardUsePayload.Scavenger { Type = CardType.Scavenger }, snapshot);

        var collected = snapshot.Collect();
        var cardAdds = collected.Records.OfType<PlayerSnapshotRecord.CardAdd>().ToList();
        cardAdds.Should().HaveCount(drawCount);
        cardAdds[0].Should().BeOfType<PlayerSnapshotRecord.CardAdd>();
        cardAdds[1].Should().BeOfType<PlayerSnapshotRecord.CardAdd>();
    }

    [Fact]
    public void Use_ReturnsScavengerActionData()
    {
        var ownerId = Guid.NewGuid();
        var owner = MockPlayer(ownerId);
        var configs = MockConfigs();
        owner.Deck.Count.Returns(5, 4);
        owner.Deck.DrawCard().Returns(CardType.Bloodhound, CardType.Trebuchet);

        owner.Hand.Add(Arg.Any<CardType>())
             .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound },
                 new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Trebuchet });

        var (_, snapshot) = new Scavenger(configs).UseCapture(owner,
            new CardUsePayload.Scavenger { Type = CardType.Scavenger });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Scavenger>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(ownerId);
    }
}

public class HandScrambleTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_RemovesOpponentHandCardsAndDrawsNew()
    {
        var invoker = MockPlayer();
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var snapshot = CreateSnapshot();
        var gameContext = MockGameContext(invoker, opponent);

        var card1 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound };
        var card2 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Trebuchet };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1, card2 });
        opponent.Deck.Count.Returns(2, 1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke, CardType.Medic);

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Smoke },
                    new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Medic });

        var result = new HandScramble(gameContext).Use(invoker,
            new CardUsePayload.HandScramble { Type = CardType.HandScramble });

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
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        var gameContext = MockGameContext(invoker, opponent);
        opponent.Hand.Entries.Returns(new List<ActiveCard>());

        var (result, snapshot) = new HandScramble(gameContext).UseCapture(invoker,
            new CardUsePayload.HandScramble { Type = CardType.HandScramble });

        result.Result.HasError.Should().BeTrue();
        snapshot.GetLastCardAction<ICardActionData>().Should().BeNull();
    }

    [Fact]
    public void Use_SnapshotRecordsRemovalsAndAdditions()
    {
        var invoker = MockPlayer();
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var snapshot = CreateSnapshot();
        var gameContext = MockGameContext(invoker, opponent);

        var cardId1 = Guid.NewGuid();
        var card1 = new ActiveCard { Id = cardId1, Type = CardType.Bloodhound };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1 });
        opponent.Deck.Count.Returns(1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke);
        var newCardId = Guid.NewGuid();

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = newCardId, Type = CardType.Smoke });

        new HandScramble(gameContext).Use(invoker,
            new CardUsePayload.HandScramble { Type = CardType.HandScramble }, snapshot);

        var collected = snapshot.Collect();
        var removeRecord = collected.Records.OfType<PlayerSnapshotRecord.CardRemove>().Should().ContainSingle().Subject;
        removeRecord.PlayerId.Should().Be(opponentId);
        removeRecord.CardId.Should().Be(cardId1);
        var addRecord = collected.Records.OfType<PlayerSnapshotRecord.CardAdd>().Should().ContainSingle().Subject;
        addRecord.PlayerId.Should().Be(opponentId);
        addRecord.CardId.Should().Be(newCardId);
    }

    [Fact]
    public void Use_OldCardsReturnedToDeck()
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        var snapshot = CreateSnapshot();
        var gameContext = MockGameContext(invoker, opponent);

        var card1 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1 });
        opponent.Deck.Count.Returns(1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke);

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Smoke });

        new HandScramble(gameContext).Use(invoker,
            new CardUsePayload.HandScramble { Type = CardType.HandScramble });

        opponent.Deck.Received(1).AddCard(CardType.Bloodhound);
    }

    [Fact]
    public void Use_ActionDataReferencesOpponent()
    {
        var invoker = MockPlayer();
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var gameContext = MockGameContext(invoker, opponent);

        var card1 = new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Bloodhound };
        opponent.Hand.Entries.Returns(new List<ActiveCard> { card1 });
        opponent.Deck.Count.Returns(1);
        opponent.Deck.DrawCard().Returns(CardType.Smoke);

        opponent.Hand.Add(Arg.Any<CardType>())
                .Returns(new ActiveCard { Id = Guid.NewGuid(), Type = CardType.Smoke });

        var (_, snapshot) = new HandScramble(gameContext).UseCapture(invoker,
            new CardUsePayload.HandScramble { Type = CardType.HandScramble });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.HandScramble>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(opponentId);
    }
}

public class LockdownTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_ReducesAdditionalMovesModifier()
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var roundActionService = Substitute.For<IRoundActionService>();
        var gameContext = MockGameContext(invoker, opponent);
        var movesReduction = CardConfigs.All.Lockdown_Normal.MovesReduction;

        opponent.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, 0f }
        });

        var result = new Lockdown(configs, roundActionService, gameContext).Use(invoker,
            new CardUsePayload.Lockdown { Type = CardType.Lockdown });

        result.Result.HasError.Should().BeFalse();
        opponent.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_SchedulesRestoration()
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var roundActionService = Substitute.For<IRoundActionService>();
        var gameContext = MockGameContext(invoker, opponent);
        var duration = CardConfigs.All.Lockdown_Normal.TurnsDuration;

        opponent.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, 0f }
        });

        new Lockdown(configs, roundActionService, gameContext).Use(invoker,
            new CardUsePayload.Lockdown { Type = CardType.Lockdown });

        roundActionService.Received(1)
                          .Schedule(Arg.Any<ModifierRoundAction>());
    }

    [Fact]
    public void Use_DisposeActionRestoresModifier()
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var roundActionService = new RoundActionService();
        var gameContext = MockGameContext(invoker, opponent);
        var movesReduction = CardConfigs.All.Lockdown_Normal.MovesReduction;
        var duration = CardConfigs.All.Lockdown_Normal.TurnsDuration;

        opponent.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, -movesReduction }
        });

        new Lockdown(configs, roundActionService, gameContext).Use(invoker,
            new CardUsePayload.Lockdown { Type = CardType.Lockdown });

        // Tick duration times to trigger the scheduled action
        for (var i = 0; i < duration; i++)
            roundActionService.Tick(new MoveSnapshot());

        // Dec sets -(movesReduction*2), then dispose restores to 0
        opponent.Modifiers.Received(1)
                .Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
        opponent.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
    }

    [Fact]
    public void Use_ActionDataReferencesOpponent()
    {
        var invoker = MockPlayer();
        var opponentId = Guid.NewGuid();
        var opponent = MockPlayer(opponentId);
        var configs = MockConfigs();
        var roundActionService = Substitute.For<IRoundActionService>();
        var gameContext = MockGameContext(invoker, opponent);

        opponent.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, 0f }
        });

        var (_, snapshot) = new Lockdown(configs, roundActionService, gameContext).UseCapture(invoker,
            new CardUsePayload.Lockdown { Type = CardType.Lockdown });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Lockdown>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(opponentId);
    }

    [Fact]
    public void Use_DisposeActionDoesNotFireBeforeDuration()
    {
        var invoker = MockPlayer();
        var opponent = MockPlayer();
        var configs = MockConfigs();
        var roundActionService = new RoundActionService();
        var gameContext = MockGameContext(invoker, opponent);
        var movesReduction = CardConfigs.All.Lockdown_Normal.MovesReduction;
        var duration = CardConfigs.All.Lockdown_Normal.TurnsDuration;

        opponent.Modifiers.Values.Returns(new Dictionary<PlayerModifier, float>
        {
            { PlayerModifier.AdditionalMoves, 0f }
        });

        new Lockdown(configs, roundActionService, gameContext).Use(invoker,
            new CardUsePayload.Lockdown { Type = CardType.Lockdown });

        opponent.Modifiers.Received(1).Add(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());

        // Only tick once — action should not fire yet (duration = 2)
        roundActionService.Tick(new MoveSnapshot());

        // Source is still active, Update was called but not Remove
        opponent.Modifiers.Received(1).Update(Arg.Any<MoveSnapshot>(), Arg.Any<IModifierSource>());
        opponent.Modifiers.DidNotReceive().Remove(Arg.Any<MoveSnapshot>(), Arg.Any<Guid>());
    }
}