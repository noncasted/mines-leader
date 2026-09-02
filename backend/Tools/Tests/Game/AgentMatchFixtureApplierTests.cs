using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Boards;
using Game.Session;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class AgentMatchFixtureApplierTests
{
    [Fact]
    public void Apply_PadsShortLayout_AndEnsureGeneratedDoesNotMoveMine()
    {
        var options = Options.Create(new BoardOptions { Size = 3, Mines = 0 });
        var board = new Board(Guid.NewGuid(), options);

        BoardLayoutParser.Apply(board, "m t");

        board.Cells.Count.Should().Be(9);
        board.Cells[new Position(0, 0)].AsTaken().HasMine.Should().BeTrue();
        board.Cells[new Position(1, 0)].AsTaken().HasMine.Should().BeFalse();
        board.Cells[new Position(1, 0)].Status.Should().Be(CellStatus.Taken);
        board.Cells[new Position(2, 0)].AsTaken().HasMine.Should().BeFalse();

        board.EnsureGenerated(new MoveSnapshot(), new Position(1, 1));

        board.Cells[new Position(0, 0)].AsTaken().HasMine.Should().BeTrue();
        board.Cells.Count.Should().Be(9);
    }

    [Fact]
    public void Apply_LayoutWiderThanSize_Throws()
    {
        var options = Options.Create(new BoardOptions { Size = 2, Mines = 0 });
        var board = new Board(Guid.NewGuid(), options);

        var act = () => BoardLayoutParser.Apply(board, "m t t");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ClosedMine_DefaultObservation_DoesNotLeakHasMine()
    {
        var fixture = CreatePlayers(boardSize: 3);
        BoardLayoutParser.Apply(fixture.Self.Board, "m t");

        var observation = AgentObservationBuilder.Build(
            fixture.Context,
            fixture.SelfId,
            Array.Empty<string>(),
            "action",
            oracle: false,
            false,
            string.Empty,
            fixture.SelfId,
            CardConfigs.All);

        var cell = observation.Self.Cells.Should().ContainSingle(c => c.X == 0 && c.Y == 0).Subject;
        cell.Status.Should().Be("closed");
        cell.HasMine.Should().BeFalse();
    }

    [Fact]
    public void ApplyHand_AfterRestoreCards_ReplacesWithBloodhound()
    {
        var fixture = CreatePlayers(boardSize: 3);
        fixture.Self.Hand.SetSize(5);
        fixture.Self.Deck.Init(5);

        var snapshot = new MoveSnapshot();
        var roundPlayers = new RoundPlayers(fixture.Context);
        roundPlayers.RestoreCards(fixture.Self, snapshot);
        fixture.Self.Hand.Entries.Count.Should().Be(5);

        AgentMatchFixtureApplier.Apply(
            fixture.Context,
            new AgentMatchFixture { SelfHand = { CardType.Bloodhound } },
            snapshot);

        fixture.Self.Hand.Entries.Should().ContainSingle();
        fixture.Self.Hand.Entries[0].Type.Should().Be(CardType.Bloodhound);
    }

    [Fact]
    public void ApplyDecks_ReplacesHumanAndBotDecks()
    {
        var fixture = CreatePlayers(boardSize: 3);
        fixture.Self.Deck.Init(3);
        fixture.Opponent.Deck.Init(3);

        AgentMatchFixtureApplier.ApplyDecks(
            fixture.Context,
            new AgentMatchFixture
            {
                SelfDeck = { CardType.Bloodhound, CardType.Medic },
                BotDeck = { CardType.Trebuchet }
            },
            new MoveSnapshot());

        fixture.Self.Deck.Count.Should().Be(2);
        fixture.Self.Deck.Peek(0).Should().Be(CardType.Bloodhound);
        fixture.Opponent.Deck.Count.Should().Be(1);
        fixture.Opponent.Deck.Peek(0).Should().Be(CardType.Trebuchet);
    }

    [Fact]
    public void SelfHandAndDeck_SkipRestore_LeavesDeckIntact()
    {
        var players = CreatePlayers(boardSize: 3);
        players.Self.Hand.SetSize(5);
        players.Self.Deck.Init(5);

        var match = new AgentMatchFixture
        {
            SelfDeck = { CardType.Bloodhound, CardType.Medic, CardType.Shield },
            SelfHand = { CardType.Sonar }
        };

        var snapshot = new MoveSnapshot();
        AgentMatchFixtureApplier.ApplyDecks(players.Context, match, snapshot);
        AgentMatchFixtureApplier.ShouldSkipRestore(players.Self, match).Should().BeTrue();
        AgentMatchFixtureApplier.ShouldSkipRestore(players.Opponent, match).Should().BeFalse();
        AgentMatchFixtureApplier.Apply(players.Context, match, snapshot);

        players.Self.Hand.Entries.Should().ContainSingle(card => card.Type == CardType.Sonar);
        players.Self.Deck.Count.Should().Be(3);
        players.Self.Deck.Peek(0).Should().Be(CardType.Bloodhound);
    }

    [Fact]
    public void SelfDeckOnly_RestoreDrawsFromFixtureDeck()
    {
        var players = CreatePlayers(boardSize: 3);
        players.Self.Hand.SetSize(2);

        var match = new AgentMatchFixture
        {
            SelfDeck = { CardType.Bloodhound, CardType.Medic, CardType.Shield }
        };

        var snapshot = new MoveSnapshot();
        AgentMatchFixtureApplier.ApplyDecks(players.Context, match, snapshot);
        new RoundPlayers(players.Context).RestoreCards(players.Self, snapshot);

        players.Self.Hand.Entries.Select(card => card.Type).Should().Equal(CardType.Bloodhound, CardType.Medic);
        players.Self.Deck.Count.Should().Be(1);
        players.Self.Deck.Peek(0).Should().Be(CardType.Shield);
    }

    [Fact]
    public void MatchBotProfile_FixtureOverridesClusterProfile()
    {
        var config = Substitute.For<IBotConfig>();
        config.Value.Returns(new BotConfigOptions
        {
            CurrentProfile = BotProfile.Medium,
            Profiles = new Dictionary<BotProfile, BotProfileConfig>
            {
                [BotProfile.Easy] = new() { CellsOpenPerRound = 2 },
                [BotProfile.Medium] = new() { CellsOpenPerRound = 4 },
                [BotProfile.Hard] = new() { CellsOpenPerRound = 8 }
            }
        });

        MatchBotProfile.Resolve(
                new MatchCreateOptions { Type = GameMatchType.LastManStandingTurnBased },
                config)
            .Should().Be(BotProfile.Medium);

        MatchBotProfile.Resolve(
                new MatchCreateOptions
                {
                    Type = GameMatchType.LastManStandingTurnBased,
                    Fixture = new AgentMatchFixture { BotProfile = BotProfile.Hard }
                },
                config)
            .Should().Be(BotProfile.Hard);

        MatchBotProfile.ResolveConfig(
                new MatchCreateOptions
                {
                    Type = GameMatchType.LastManStandingTurnBased,
                    Fixture = new AgentMatchFixture { BotProfile = BotProfile.Easy }
                },
                config)
            .CellsOpenPerRound.Should().Be(2);
    }

    private static PlayersFixture CreatePlayers(int boardSize)
    {
        var selfId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var selfBoard = new Board(selfId, Options.Create(new BoardOptions { Size = boardSize, Mines = 0 }));
        var opponentBoard = new Board(opponentId, Options.Create(new BoardOptions { Size = boardSize, Mines = 0 }));

        var self = CreatePlayer(selfId, selfBoard, isBot: false);
        var opponent = CreatePlayer(opponentId, opponentBoard, isBot: true);

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

        return new PlayersFixture(context, selfId, self, opponent);
    }

    private static IPlayer CreatePlayer(Guid id, IBoard board, bool isBot)
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
        player.Hand.Returns(new Hand());
        player.Deck.Returns(new Deck(new[] { CardType.Medic }));
        player.Stash.Returns(new Stash());
        player.Modifiers.Returns(modifiers);
        return player;
    }

    private sealed record PlayersFixture(
        IGameContext Context,
        Guid SelfId,
        IPlayer Self,
        IPlayer Opponent);
}
