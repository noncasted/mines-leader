using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class AgentObservationBuilderTests
{
    [Fact]
    public void ClosedTakenMine_OracleFalse_DoesNotLeakHasMine()
    {
        var fixture = CreateFixture(selfMines: [(1, 1)]);

        var observation = Build(fixture, oracle: false);
        var cell = observation.Self.Cells.Should().ContainSingle(c => c.X == 1 && c.Y == 1).Subject;

        cell.Status.Should().Be("closed");
        cell.HasMine.Should().BeFalse();
    }

    [Fact]
    public void ClosedTakenMine_OracleTrue_SetsHasMine()
    {
        var fixture = CreateFixture(selfMines: [(1, 1)]);

        var observation = Build(fixture, oracle: true);
        var cell = observation.Self.Cells.Should().ContainSingle(c => c.X == 1 && c.Y == 1).Subject;

        cell.Status.Should().Be("closed");
        cell.HasMine.Should().BeTrue();
    }

    [Fact]
    public void OpenSafeCell_StatusOpenWithMinesAroundAndAsciiDigit()
    {
        var fixture = CreateFixture(selfMines: [(0, 0)], selfFree: [(1, 1)]);

        var observation = Build(fixture, oracle: false);
        var cell = observation.Self.Cells.Should().ContainSingle(c => c.X == 1 && c.Y == 1).Subject;

        cell.Status.Should().Be("open");
        cell.MinesAround.Should().Be(1);
        cell.HasMine.Should().BeFalse();

        var lines = observation.Self.BoardAscii.Split('\n');
        lines[1][1].Should().Be('1');
    }

    [Fact]
    public void OpponentHand_OracleFalse_HidesCardType()
    {
        var fixture = CreateFixture();

        var observation = Build(fixture, oracle: false);

        observation.Opponent.Hand.Should().ContainSingle();
        observation.Opponent.Hand[0].Id.Should().Be(fixture.OpponentCardId);
        observation.Opponent.Hand[0].Type.Should().Be("?");
        observation.Opponent.Hand[0].ManaCost.Should().Be(0);
        observation.Opponent.Hand[0].Target.Should().BeEmpty();
        observation.Opponent.Hand[0].Shape.Should().BeEmpty();
        observation.Opponent.Hand[0].Size.Should().Be(0);
        observation.Opponent.Hand[0].NeedsPosition.Should().BeFalse();
        observation.Opponent.Hand[0].Summary.Should().BeEmpty();
    }

    [Fact]
    public void SelfHand_Medic_DescribesSelfTargetWithoutPosition()
    {
        var fixture = CreateFixture();

        var observation = Build(fixture, oracle: false);
        var card = observation.Self.Hand.Should().ContainSingle().Subject;

        card.Target.Should().Be(nameof(CardTarget.Self));
        card.Shape.Should().Be(AgentCardCatalog.ShapeNone);
        card.Size.Should().Be(0);
        card.NeedsPosition.Should().BeFalse();
        card.Summary.Should().Be(AgentCardCatalog.Get(CardType.Medic).Summary);
    }

    [Fact]
    public void SelfHand_Trebuchet_DescribesOpponentBoardRhombus()
    {
        var fixture = CreateFixture(selfCardType: CardType.Trebuchet);

        var observation = Build(fixture, oracle: false);
        var card = observation.Self.Hand.Should().ContainSingle().Subject;

        card.Type.Should().Be(nameof(CardType.Trebuchet));
        card.Target.Should().Be(nameof(CardTarget.OpponentBoard));
        card.Shape.Should().Be(AgentCardCatalog.ShapeRhombus);
        card.Size.Should().Be(CardConfigs.All.Trebuchet_Normal.Size);
        card.NeedsPosition.Should().BeTrue();
        card.Summary.Should().NotBeEmpty();
    }

    [Fact]
    public void SelfHand_UsesRealCardType()
    {
        var fixture = CreateFixture();

        var observation = Build(fixture, oracle: false);

        observation.Self.Hand.Should().ContainSingle();
        observation.Self.Hand[0].Id.Should().Be(fixture.SelfCardId);
        observation.Self.Hand[0].Type.Should().Be(nameof(CardType.Medic));
        observation.Self.Hand[0].ManaCost.Should().Be(CardConfigs.All.All[CardType.Medic].ManaCost);
    }

    [Fact]
    public void ObservationEventBuffer_TakeAfter_ReturnsOnlyNewLines()
    {
        var buffer = new ObservationEventBuffer();
        buffer.Append("one");
        buffer.Append("two");

        var all = buffer.TakeAfter(-1);
        all.Should().Equal("one", "two");

        var afterFirst = buffer.TakeAfter(0);
        afterFirst.Should().Equal("two");

        var cursor = buffer.Cursor;
        buffer.Append("three");

        buffer.TakeAfter(cursor).Should().Equal("three");
        buffer.TakeAfter(cursor).Should().Equal("three");
    }

    [Fact]
    public void Publisher_NonTurnBased_DoesNotSend()
    {
        var fixture = CreateFixture();
        var services = Substitute.For<IServiceProvider>();
        var cardConfigs = Substitute.For<ICardConfigs>();
        var publisher = new AgentObservationPublisher(
            fixture.Context,
            services,
            new ObservationEventBuffer(),
            new MatchCreateOptions { Type = GameMatchType.LastManStanding },
            cardConfigs,
            new RoundPlayers(fixture.Context));

        publisher.Publish(fixture.SelfId, "action", false, string.Empty);

        services.DidNotReceive().GetService(typeof(IGameRound));
    }

    [Fact]
    public void Publisher_CanBeResolvedWithoutIGameRound()
    {
        var context = Substitute.For<IGameContext>();
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton(Substitute.For<IObservationEventBuffer>());
        services.AddSingleton(new MatchCreateOptions { Type = GameMatchType.LastManStandingTurnBased });
        services.AddSingleton(Substitute.For<ICardConfigs>());
        services.AddSingleton(new RoundPlayers(context));
        services.AddSingleton<IAgentObservationPublisher, AgentObservationPublisher>();

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAgentObservationPublisher>().Should().NotBeNull();
    }

    [Fact]
    public void RecordReveal_OpensCells_WritesBoardRevealedLine()
    {
        var (board, target) = BoardParser.Parse("""
                                                t t t
                                                t x t
                                                t t t
                                                """);

        var logger = Substitute.For<ISessionLogger>();
        var snapshot = new MoveSnapshot { SessionLogger = logger };

        var opened = snapshot.RecordReveal(board, target);

        opened.Should().NotBeEmpty();
        logger.Received(1).LogBoardRevealed(board.OwnerId, Arg.Is<IReadOnlyList<Position>>(p => p.Count == opened.Count));
    }

    private static SharedAgentObservation Build(Fixture fixture, bool oracle)
    {
        return AgentObservationBuilder.Build(
            fixture.Context,
            fixture.SelfId,
            Array.Empty<string>(),
            "action",
            oracle,
            false,
            string.Empty,
            fixture.SelfId,
            CardConfigs.All);
    }

    private static Fixture CreateFixture(
        (int x, int y)[]? selfMines = null,
        (int x, int y)[]? selfFree = null,
        CardType selfCardType = CardType.Medic)
    {
        var selfId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var selfBoard = new TestBoardBuilder(3)
            .WithOwner(selfId)
            .WithMinesAt(selfMines ?? [])
            .WithFreeAt(selfFree ?? [])
            .Build();

        var opponentBoard = new TestBoardBuilder(3)
            .WithOwner(opponentId)
            .Build();

        var selfHand = new Hand();
        var selfCard = selfHand.Add(selfCardType);

        var opponentHand = new Hand();
        var opponentCard = opponentHand.Add(CardType.Bloodhound);

        var self = CreatePlayer(selfId, selfBoard, selfHand);
        var opponent = CreatePlayer(opponentId, opponentBoard, opponentHand);

        var users = new Dictionary<IUser, IPlayer>
        {
            { self.User, self },
            { opponent.User, opponent }
        };
        var boards = new Dictionary<IPlayer, IBoard>
        {
            { self, selfBoard },
            { opponent, opponentBoard }
        };

        var context = Substitute.For<IGameContext>();
        context.Players.Returns(new List<IPlayer> { self, opponent });
        context.UserToPlayer.Returns((IReadOnlyDictionary<IUser, IPlayer>)users);
        context.Boards.Returns((IReadOnlyDictionary<IPlayer, IBoard>)boards);

        return new Fixture(context, selfId, opponentId, selfCard.Id, opponentCard.Id);
    }

    private static IPlayer CreatePlayer(Guid id, IBoard board, IHand hand)
    {
        var user = Substitute.For<IUser>();
        user.Id.Returns(id);
        user.IsBot.Returns(false);

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

    private sealed record Fixture(
        IGameContext Context,
        Guid SelfId,
        Guid OpponentId,
        Guid SelfCardId,
        Guid OpponentCardId);
}
