using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class TrebuchetTests : PlayerCardTestsBase
{
    private static IPlayer MockOwner(float trebuchetBoost = 0f)
    {
        var player = MockPlayer();

        var values = new Dictionary<PlayerModifier, float>
            { { PlayerModifier.TrebuchetBoost, trebuchetBoost } };
        player.Modifiers.Values.Returns(values);
        return player;
    }

    private static (IPlayer invoker, IPlayer opponent, IGameContext ctx) SetupOpponent(IBoard board, float boost = 0f)
    {
        var invoker = MockOwner(boost);
        var opponent = MockPlayer();
        opponent.Board.Returns(board);
        var ctx = MockGameContext(invoker, opponent);
        return (invoker, opponent, ctx);
    }

    [Fact]
    public void Use_ConvertsFreeToTaken()
    {
        var (board, target) = BoardParser.Parse("""
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ x _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _ _ _
                                                """);
        var freeCountBefore = board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var (invoker, _, ctx) = SetupOpponent(board);
        var card = new Trebuchet(MockConfigs(), ctx);
        var result = card.Use(invoker, new CardUsePayload.Trebuchet { Position = target });

        result.Result.HasError.Should().BeFalse();
        var freeCountAfter = board.Cells.Values.Count(c => c.Status == CellStatus.Free);
        var converted = freeCountBefore - freeCountAfter;
        converted.Should().BeGreaterThanOrEqualTo(4);
        converted.Should().BeLessThanOrEqualTo(12);
    }

    [Fact]
    public void Use_AllCellsAlreadyTaken_Fails()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);
        var (invoker, _, ctx) = SetupOpponent(board);
        var card = new Trebuchet(MockConfigs(), ctx);
        var result = card.Use(invoker, new CardUsePayload.Trebuchet { Position = target });

        result.Result.HasError.Should().BeTrue();
    }

    [Fact]
    public void Use_ResetsTrebuchetBoostAfterUse()
    {
        var (board, _) = BoardParser.Parse("""
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           _ _ _ _ _ _ _ _
                                           """);
        var (invoker, _, ctx) = SetupOpponent(board, 1f);
        var card = new Trebuchet(MockConfigs(), ctx);
        card.Use(invoker, new CardUsePayload.Trebuchet { Position = new Position(4, 4) });

        invoker.Modifiers.Received(1).Set(PlayerModifier.TrebuchetBoost, 0f);
    }
}