using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

public class AgentLegalPlaysBuilderTests
{
    [Fact]
    public void Medic_NeedsNoPosition_AndEmptyCells()
    {
        var fixture = CreateFixture(selfTypes: [CardType.Medic]);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.Type.Should().Be(nameof(CardType.Medic));
        card.NeedsPosition.Should().BeFalse();
        card.Cells.Should().BeEmpty();
        card.Error.Should().BeEmpty();
    }

    [Fact]
    public void Bloodhound_TakenBoard_HasLegalCells()
    {
        var board = new TestBoardBuilder(5).Build();
        var fixture = CreateFixture(selfTypes: [CardType.Bloodhound], selfBoard: board);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.Type.Should().Be(nameof(CardType.Bloodhound));
        card.NeedsPosition.Should().BeTrue();
        card.Cells.Should().NotBeEmpty();
        card.Error.Should().BeEmpty();
        card.Cells.Should().OnlyContain(cell => cell.X >= 0 && cell.X < 5 && cell.Y >= 0 && cell.Y < 5);
    }

    [Fact]
    public void Bloodhound_FullyFreeBoard_HasNoCells()
    {
        var free = new (int x, int y)[25];
        var index = 0;
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
                free[index++] = (x, y);
        }

        var board = new TestBoardBuilder(5).WithFreeAt(free).Build();
        var fixture = CreateFixture(selfTypes: [CardType.Bloodhound], selfBoard: board);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.NeedsPosition.Should().BeTrue();
        card.Cells.Should().BeEmpty();
        card.Error.Should().BeEmpty();
    }

    [Fact]
    public void Recycler_ExtraCardIds_ContainsOtherHandCard()
    {
        var fixture = CreateFixture(selfTypes: [CardType.Recycler, CardType.Medic]);

        var response = Build(fixture);
        var recycler = response.Cards.Should().ContainSingle(c => c.Type == nameof(CardType.Recycler)).Subject;
        var medic = response.Cards.Should().ContainSingle(c => c.Type == nameof(CardType.Medic)).Subject;

        recycler.NeedsExtraCard.Should().BeTrue();
        recycler.ExtraCardIds.Should().ContainSingle().Which.Should().Be(medic.Id);
        recycler.ExtraCardIds.Should().NotContain(recycler.Id);
    }

    [Fact]
    public void ClosedMine_IsNotExposedOnLegalPlays()
    {
        var board = new TestBoardBuilder(3).WithMinesAt((0, 0)).Build();
        var fixture = CreateFixture(selfTypes: [CardType.Bloodhound], selfBoard: board);

        var json = System.Text.Json.JsonSerializer.Serialize(Build(fixture));
        json.Should().NotContain("HasMine");
        json.ToLowerInvariant().Should().NotContain("\"hasmine\"");
    }

    private static SharedAgentLegalPlaysResponse Build(Fixture fixture)
    {
        return AgentLegalPlaysBuilder.Build(
            fixture.Context,
            fixture.SelfId,
            fixture.SelfId,
            CardConfigs.All);
    }

    private static Fixture CreateFixture(CardType[] selfTypes, IBoard? selfBoard = null)
    {
        var selfId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var board = selfBoard ?? new TestBoardBuilder(3).WithOwner(selfId).Build();
        var opponentBoard = new TestBoardBuilder(3).WithOwner(opponentId).Build();

        var hand = new Hand();
        foreach (var type in selfTypes)
            hand.Add(type);

        var opponentHand = new Hand();
        opponentHand.Add(CardType.Medic);

        var self = CreatePlayer(selfId, board, hand, isBot: false);
        var opponent = CreatePlayer(opponentId, opponentBoard, opponentHand, isBot: true);

        var users = new Dictionary<IUser, IPlayer>
        {
            { self.User, self },
            { opponent.User, opponent }
        };
        var boards = new Dictionary<IPlayer, IBoard>
        {
            { self, board },
            { opponent, opponentBoard }
        };

        var context = Substitute.For<IGameContext>();
        context.Players.Returns(new List<IPlayer> { self, opponent });
        context.UserToPlayer.Returns((IReadOnlyDictionary<IUser, IPlayer>)users);
        context.Boards.Returns((IReadOnlyDictionary<IPlayer, IBoard>)boards);

        return new Fixture(context, selfId);
    }

    private static IPlayer CreatePlayer(Guid id, IBoard board, IHand hand, bool isBot)
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

    private sealed record Fixture(IGameContext Context, Guid SelfId);
}
