using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class BloodhoundTests : PlayerCardTestsBase
{
    private static IPlayer MockInvoker(IBoard board)
    {
        var player = MockPlayer();
        player.Board.Returns(board);
        return player;
    }

    [Fact]
    public void Use_OpensTargetAreaAndFloodFills()
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
        var invoker = MockInvoker(board);
        var card = new Bloodhound(MockConfigs());
        card.Use(invoker, new CardUsePayload.Bloodhound { Position = target });

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
    public void Use_MinesDenselyPacked_LimitsReveal()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t t t
                                                t t m m m t t
                                                t m t t t m t
                                                t m t x t m t
                                                t m t t t m t
                                                t t m m m t t
                                                t t t t t t t
                                                """);
        var invoker = MockInvoker(board);
        var card = new Bloodhound(MockConfigs());
        card.Use(invoker, new CardUsePayload.Bloodhound { Position = target });

        BoardParser.AssertBoard(board, """
                                       t t t t t t t
                                       t t m m m t t
                                       t m R R R m t
                                       t m R R R m t
                                       t m R R R m t
                                       t t m m m t t
                                       t t t t t t t
                                       """);
    }

    [Fact]
    public void Use_AllTakenAlreadyFreeInCross_Fails()
    {
        var (board, _) = BoardParser.Parse("""
                                            t t t t t
                                            t t _ t t
                                            t _ _ _ t
                                            t t _ t t
                                            t t t t t
                                            """);
        var invoker = MockInvoker(board);
        var card = new Bloodhound(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Bloodhound { Position = new Position(2, 2) });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_AtCorner_OpensVisibleArea()
    {
        var (board, target) = BoardParser.Parse("""
                                                x t t t t t
                                                t t t t t t
                                                t t m t t t
                                                t t t t m t
                                                t t t t t t
                                                t t t t t t
                                                """);
        var invoker = MockInvoker(board);
        var card = new Bloodhound(MockConfigs());
        card.Use(invoker, new CardUsePayload.Bloodhound { Position = target });

        BoardParser.AssertBoard(board, """
                                       R R R R R R
                                       R R R R R R
                                       R R m R R R
                                       R R R R m t
                                       R R R R R R
                                       R R R R R R
                                       """);
    }

    [Fact]
    public void Use_ActionDataHasTargetPlayer()
    {
        var ownerId = Guid.NewGuid();
        var board = new TestBoardBuilder(5).WithOwner(ownerId).WithMinesAt((0, 0)).Build();
        var invoker = MockInvoker(board);
        var card = new Bloodhound(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Bloodhound { Position = new Position(2, 2) });

        var snapshot = result.ActionData as CardActionSnapshot.Bloodhound;
        snapshot.Should().NotBeNull();
        snapshot!.TargetPlayer.Should().Be(ownerId);
    }
}
