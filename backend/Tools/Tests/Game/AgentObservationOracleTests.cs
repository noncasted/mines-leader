using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Snapshots;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// The oracle of the agent observation (mines of closed cells, the hand of the opponent) is a cheat for the agent
/// play: only with ICheatsAccess and only against a bot. A PvP observation never shows a mine under a closed cell.
/// </summary>
public class AgentObservationOracleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pvp_OracleRequested_NoHiddenState(bool cheatsAllowed)
    {
        var fixture = CreateFixture(opponentIsBot: false, cheatsAllowed);

        var observation = Publish(fixture);

        AssertNoHiddenState(observation);
    }

    [Fact]
    public void AgainstBot_OracleRequested_WithoutAccess_NoHiddenState()
    {
        var fixture = CreateFixture(opponentIsBot: true, cheatsAllowed: false);

        var observation = Publish(fixture);

        AssertNoHiddenState(observation);
    }

    [Fact]
    public void AgainstBot_OracleRequested_WithAccess_ShowsMines()
    {
        var fixture = CreateFixture(opponentIsBot: true, cheatsAllowed: true);

        var observation = Publish(fixture);

        observation.Self.Cells.Single(c => c.X == 1 && c.Y == 1).HasMine.Should().BeTrue();
        observation.Opponent.Cells.Single(c => c.X == 2 && c.Y == 2).HasMine.Should().BeTrue();
        observation.Opponent.Hand.Should().ContainSingle().Which.Type.Should().Be(CardType.Bloodhound.ToString());
    }

    private static void AssertNoHiddenState(SharedAgentObservation observation)
    {
        observation.Self.Cells.Should().NotBeEmpty();
        observation.Opponent.Cells.Should().NotBeEmpty();

        observation.Self.Cells.Where(c => c.Status != "open").Should().OnlyContain(c => c.HasMine == false);
        observation.Opponent.Cells.Where(c => c.Status != "open").Should().OnlyContain(c => c.HasMine == false);
        observation.Opponent.Hand.Should().OnlyContain(card => card.Type != CardType.Bloodhound.ToString());
    }

    private static SharedAgentObservation Publish(Fixture fixture)
    {
        SharedAgentObservation? sent = null;
        fixture.Viewer.Connection.Send(Arg.Do<INetworkContext>(context => sent = (SharedAgentObservation)context));

        fixture.Publisher.Publish(fixture.Viewer.Id, "action", false, string.Empty, oracle: true);

        sent.Should().NotBeNull();
        return sent!;
    }

    private static Fixture CreateFixture(bool opponentIsBot, bool cheatsAllowed)
    {
        var selfId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var selfBoard = new TestBoardBuilder(3).WithOwner(selfId).WithMinesAt((1, 1)).WithFreeAt((0, 0)).Build();
        var opponentBoard = new TestBoardBuilder(3).WithOwner(opponentId).WithMinesAt((2, 2)).Build();

        var opponentHand = new Hand();
        opponentHand.Add(CardType.Bloodhound);

        var self = CreatePlayer(selfId, isBot: false, selfBoard, new Hand());
        var opponent = CreatePlayer(opponentId, opponentIsBot, opponentBoard, opponentHand);

        var users = new Dictionary<IUser, IPlayer>
        {
            { self.User, self },
            { opponent.User, opponent }
        };

        var context = Substitute.For<IGameContext>();
        context.Players.Returns(new List<IPlayer> { self, opponent });
        context.UserToPlayer.Returns(users);

        var cardConfigs = Substitute.For<ICardConfigs>();
        cardConfigs.Value.Returns(CardConfigs.All);

        var access = Substitute.For<ICheatsAccess>();
        access.IsAllowed.Returns(cheatsAllowed);

        var publisher = new AgentObservationPublisher(
            context,
            Substitute.For<IServiceProvider>(),
            new ObservationEventBuffer(),
            new MatchCreateOptions { Type = GameMatchType.LastManStandingTurnBased },
            cardConfigs,
            new RoundPlayers(context),
            access);

        return new Fixture(publisher, self.User);
    }

    private static IPlayer CreatePlayer(Guid id, bool isBot, IBoard board, IHand hand)
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(id);
        user.IsBot.Returns(isBot);

        var modifiers = new Modifiers();
        var health = new Health(modifiers);
        health.SetMax(new MoveSnapshot(), 3);
        health.SetCurrent(new MoveSnapshot(), 3);

        var mana = new Mana(modifiers);
        mana.SetMax(new MoveSnapshot(), 4);
        mana.SetCurrent(new MoveSnapshot(), 4);

        var moves = new Moves(modifiers);
        moves.SetMax(new MoveSnapshot(), 5);
        moves.Restore(new MoveSnapshot());

        var player = Substitute.For<IPlayer>();
        player.User.Returns(user);
        player.Board.Returns(board);
        player.Health.Returns(health);
        player.Mana.Returns(mana);
        player.Moves.Returns(moves);
        player.Hand.Returns(hand);
        player.Deck.Returns(new Deck(new[] { CardType.Medic }));
        player.Stash.Returns(new Stash());
        player.Modifiers.Returns(modifiers);
        return player;
    }

    private sealed record Fixture(AgentObservationPublisher Publisher, IUser Viewer);
}
