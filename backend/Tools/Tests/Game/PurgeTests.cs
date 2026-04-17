using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class PurgeTests : PlayerCardTestsBase
{
    private static IPlayer MockOwner(IBoard board)
    {
        var player = MockPlayer();
        player.Board.Returns(board);
        return player;
    }

    [Fact]
    public void Use_RemovesAllEffects()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """);
        board.Cells[new Position(1, 1)].AddEffect(new SmokeEffect { Id = Guid.NewGuid() });
        board.Cells[new Position(2, 2)].AddEffect(new FogEffect { Id = Guid.NewGuid() });

        var owner = MockOwner(board);
        var card = new Purge();
        var result = card.Use(owner, new CardUsePayload.Purge { Type = CardType.Purge });

        result.Result.HasError.Should().BeFalse();
        board.Cells.Values.SelectMany(c => c.Effects).Should().BeEmpty();
    }

    [Fact]
    public void Use_NoEffects_Succeeds()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """);
        var owner = MockOwner(board);
        var card = new Purge();
        var result = card.Use(owner, new CardUsePayload.Purge { Type = CardType.Purge });

        result.Result.HasError.Should().BeFalse();
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var (board, _) = BoardParser.Parse("""
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           t t t t t
                                           """);
        var owner = MockOwner(board);
        var card = new Purge();
        var (_, snapshot) = card.UseCapture(owner, new CardUsePayload.Purge { Type = CardType.Purge });

        var actionData = snapshot.GetLastCardAction<CardActionSnapshot.Purge>();
        actionData.Should().NotBeNull();
        actionData!.TargetPlayer.Should().Be(owner.User.Id);
    }
}