using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class SonarTests : PlayerCardTestsBase
{
    private static IPlayer MockInvoker(IBoard board)
    {
        var player = MockPlayer();
        player.Board.Returns(board);
        return player;
    }

    [Fact]
    public void Use_FlagsMineInRange()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t m t t t t
                                                t t m t t t t t t t
                                                t t t t t t t m t t
                                                t t t m t t t t t t
                                                t m t t x t t t t t
                                                t t t t t t t t m t
                                                t t t t t t m t t t
                                                t t m t t t t t t t
                                                t t t t t t t t t t
                                                t t t t m t t t m t
                                                """);
        var invoker = MockInvoker(board);
        var card = new Sonar(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Sonar { Position = target });

        result.Result.HasError.Should().BeFalse();

        BoardParser.AssertBoard(board, """
                                       t t t t t m t t t t
                                       t t m t t t t t t t
                                       t t t t t t t m t t
                                       t t t f t t t t t t
                                       t m t t t t t t t t
                                       t t t t t t t t m t
                                       t t t t t t m t t t
                                       t t m t t t t t t t
                                       t t t t t t t t t t
                                       t t t t m t t t m t
                                       """);
    }

    [Fact]
    public void Use_NoMines_Fails()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);
        var invoker = MockInvoker(board);
        var card = new Sonar(MockConfigs());
        var result = card.Use(invoker, new CardUsePayload.Sonar { Position = target });

        result.Result.HasError.Should().BeTrue();
    }
}