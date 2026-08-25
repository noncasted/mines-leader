using FluentAssertions;
using Game.GamePlay;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class MirrorMatchTests : PlayerCardTestsBase
{
    [Fact]
    public void Use_NoOpponentCard_Fails()
    {
        var (card, invoker, _) = CreateCard();

        var result = card.Use(invoker, new CardUsePayload.MirrorMatch { Type = CardType.MirrorMatch });

        result.Result.HasError.Should().BeTrue();
        result.Result.Message.Should().Contain("has not used any card");
    }

    [Fact]
    public void Use_OpponentLastCardIsMirrorMatch_Fails()
    {
        var (card, invoker, opponent) = CreateCard();
        opponent.Actions.LastUsedCardType.Returns(CardType.MirrorMatch);
        opponent.Actions.LastUsedPayload.Returns(new CardUsePayload.MirrorMatch { Type = CardType.MirrorMatch });

        var result = card.Use(invoker, new CardUsePayload.MirrorMatch { Type = CardType.MirrorMatch });

        result.Result.HasError.Should().BeTrue();
        result.Result.Message.Should().Contain("Cannot copy Mirror Match");
    }

    [Fact]
    public void Use_CopiesBloodhound_OpensInvokerBoard()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t m t t t t
                                                t t m t t t t t t t
                                                t t t t t t t m t t
                                                t t t t t t t t t t
                                                t m t t x t t t t t
                                                t t t t t t t t m t
                                                t t t t t t m t t t
                                                t t m t t t t t t t
                                                t t t t t t t t t t
                                                t t t t m t t t m t
                                                """);
        var (card, invoker, opponent) = CreateCard(invokerBoard: board);
        opponent.Actions.LastUsedCardType.Returns(CardType.Bloodhound);
        opponent.Actions.LastUsedPayload.Returns(new CardUsePayload.Bloodhound
        {
            Type = CardType.Bloodhound,
            Position = target
        });

        var result = card.Use(invoker, new CardUsePayload.MirrorMatch { Type = CardType.MirrorMatch });

        result.Result.HasError.Should().BeFalse();
        BoardParser.AssertBoard(board, """
                                       t t t t t m t t t t
                                       t t m R R R R t t t
                                       t t R R R R R m t t
                                       t t R R R R R R t t
                                       t m R R R R R R t t
                                       t t R R R R R R m t
                                       t t R R R R m t t t
                                       t t m R R R t t t t
                                       t t t R R R t t t t
                                       t t t t m t t t m t
                                       """);
    }

    [Fact]
    public void Use_CopiesBloodhound_RecordsSingleMirrorMatchCardUseWithCopiedAction()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t
                                                t t t t t t t
                                                t t t t t t t
                                                t t t x t t t
                                                t t t t t t t
                                                t t t t t t t
                                                t t t t t t t
                                                """);
        var ownerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var (card, invoker, opponent) = CreateCard(invokerBoard: board, invokerId: ownerId);
        opponent.Actions.LastUsedCardType.Returns(CardType.Bloodhound);
        opponent.Actions.LastUsedPayload.Returns(new CardUsePayload.Bloodhound
        {
            Type = CardType.Bloodhound,
            Position = target
        });

        var snapshot = new MoveSnapshot();
        var result = card.Use(invoker, new CardUsePayload.MirrorMatch { Type = CardType.MirrorMatch }, snapshot,
            cardId);

        result.Result.HasError.Should().BeFalse();

        var cardUses = snapshot.Collect().Records.OfType<PlayerSnapshotRecord.CardUse>().ToList();
        cardUses.Should().ContainSingle();
        cardUses[0].CardId.Should().Be(cardId);
        cardUses[0].PlayerId.Should().Be(ownerId);

        var data = cardUses[0].Data.Should().BeOfType<CardActionSnapshot.MirrorMatch>().Subject;
        data.CopiedCard.Should().Be(CardType.Bloodhound);
        data.TargetPlayer.Should().Be(ownerId);

        var copied = data.CopiedAction.Should().BeOfType<CardActionSnapshot.Bloodhound>().Subject;
        copied.OpenedCells.Should().NotBeEmpty();
        copied.TargetPlayer.Should().Be(board.OwnerId);
    }

    [Fact]
    public void Use_CopiedBloodhoundFails_DoesNotRecordCardUse()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t _ t t
                                           t _ _ _ t
                                           t t _ t t
                                           t t t t t
                                           """);
        var (card, invoker, opponent) = CreateCard(invokerBoard: board);
        opponent.Actions.LastUsedCardType.Returns(CardType.Bloodhound);
        opponent.Actions.LastUsedPayload.Returns(new CardUsePayload.Bloodhound
        {
            Type = CardType.Bloodhound,
            Position = new Position(2, 2)
        });

        var snapshot = new MoveSnapshot();
        var result = card.Use(invoker, new CardUsePayload.MirrorMatch { Type = CardType.MirrorMatch }, snapshot);

        result.Result.HasError.Should().BeTrue();
        snapshot.Collect().Records.OfType<PlayerSnapshotRecord.CardUse>().Should().BeEmpty();
    }

    [Fact]
    public void Use_CopiesMedic_NestsMedicAction()
    {
        var (card, invoker, opponent) = CreateCard();
        opponent.Actions.LastUsedCardType.Returns(CardType.Medic);
        opponent.Actions.LastUsedPayload.Returns(new CardUsePayload.Medic { Type = CardType.Medic });

        var (_, snapshot) = card.UseCapture(invoker, new CardUsePayload.MirrorMatch { Type = CardType.MirrorMatch });

        invoker.Health.Received(1).Heal(Arg.Any<MoveSnapshot>(), 1);

        var data = snapshot.GetLastCardAction<CardActionSnapshot.MirrorMatch>();
        data.Should().NotBeNull();
        data!.CopiedCard.Should().Be(CardType.Medic);
        data.CopiedAction.Should().BeOfType<CardActionSnapshot.Medic>();
        snapshot.GetLastCardAction<CardActionSnapshot.Medic>().Should().BeNull();
    }

    private static (MirrorMatch Card, IPlayer Invoker, IPlayer Opponent) CreateCard(
        IBoard? invokerBoard = null,
        Guid? invokerId = null)
    {
        var invoker = MockPlayer(invokerId);
        var opponent = MockPlayer();

        if (invokerBoard != null)
            invoker.Board.Returns(invokerBoard);

        var gameContext = MockGameContext(invoker, opponent);
        var services = new ServiceCollection();
        services.AddSingleton(MockConfigs());
        services.AddSingleton(gameContext);
        services.AddSingleton<ICard<CardUsePayload.Bloodhound>, Bloodhound>();
        services.AddSingleton<ICard<CardUsePayload.Medic>, Medic>();
        var provider = services.BuildServiceProvider();

        return (new MirrorMatch(gameContext, provider), invoker, opponent);
    }
}
