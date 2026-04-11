using FluentAssertions;
using Game.GamePlay;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class MineClusterTests
{
    [Fact]
    public void Use_PlacesMinesOnFreeCellsInCross()
    {
        // Cross(2) at (2,2): selects (2,1) and (1,2) when they are Free
        // Pattern is 2x2 grid with bottom-row and right-column as cross positions
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t _ t t
                                                t _ x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var result = new MineCluster(Substitute.For<IPlayer>(), board, CardConfigs.MineCluster,
            new CardUsePayload.MineCluster { Position = target }).Use();

        result.Result.HasError.Should().BeFalse();

        // Free cells in Cross(2) pattern get mines placed
        BoardParser.AssertBoard(board, """
                                       t t t t t
                                       t t m t t
                                       t m t t t
                                       t t t t t
                                       t t t t t
                                       """);
    }

    [Fact]
    public void Use_NoFreeCellsInPattern_Fails()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t t t
                                                t t t t t
                                                t t x t t
                                                t t t t t
                                                t t t t t
                                                """);

        var result = new MineCluster(Substitute.For<IPlayer>(), board, CardConfigs.MineCluster,
            new CardUsePayload.MineCluster { Position = target }).Use();

        result.Result.HasError.Should().BeTrue();
        result.ActionData.Should().BeNull();
    }

    [Fact]
    public void Use_ActionDataReferencesTargetBoardOwner()
    {
        var ownerId = Guid.NewGuid();
        var board = new TestBoardBuilder(5)
            .WithOwner(ownerId)
            .WithFreeAt((2, 1), (1, 2), (2, 2), (3, 2), (2, 3))
            .Build();

        var result = new MineCluster(Substitute.For<IPlayer>(), board, CardConfigs.MineCluster,
            new CardUsePayload.MineCluster { Position = new Position(2, 2) }).Use();

        var actionData = result.ActionData.Should().BeOfType<CardActionSnapshot.MineCluster>().Subject;
        actionData.TargetPlayer.Should().Be(ownerId);
    }
}
