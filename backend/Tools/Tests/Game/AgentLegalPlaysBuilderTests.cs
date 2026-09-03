using FluentAssertions;
using Game.GamePlay;
using Game.Session;
using Microsoft.Extensions.Options;
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

    public static TheoryData<CardType> PositionCardTypes()
    {
        var data = new TheoryData<CardType>();
        foreach (var type in CardTypeExtensions.All)
        {
            if (CardUsePayloadFactory.CreateDefault(type) is IBoardCardUsePayload)
                data.Add(type);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PositionCardTypes))]
    public void EveryPositionCard_HalfOpenBoards_HasCellsAndNoError(CardType type)
    {
        // Левая половина открыта, правая закрыта: и Taken-, и Free-правила находят клетки.
        var fixture = CreateFixture(
            selfTypes: [type],
            selfBoard: HalfOpenBoard(8),
            opponentBoard: HalfOpenBoard(8));

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.NeedsPosition.Should().BeTrue();
        card.Error.Should().BeEmpty($"{type} must have a target rule");
        card.Cells.Should().NotBeEmpty($"{type} must be playable somewhere on a half-open board");
        card.Cells.Should().OnlyContain(cell => cell.X >= 0 && cell.X < 8 && cell.Y >= 0 && cell.Y < 8);
    }

    [Fact]
    public void Trebuchet_OpponentBoardNotGenerated_ReturnsError()
    {
        var opponentBoard = new Board(Guid.NewGuid(), Options.Create(new BoardOptions { Size = 5, Mines = 0 }));
        var fixture = CreateFixture(selfTypes: [CardType.Trebuchet], opponentBoard: opponentBoard);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        opponentBoard.IsGenerated.Should().BeFalse();
        card.Error.Should().Be("Opponent board is not generated yet");
        card.Cells.Should().BeEmpty();
    }

    [Fact]
    public void Trebuchet_OpponentBoardGenerated_TargetsOpenCells()
    {
        // Trebuchet.Use берёт SelectFree: закрывает открытые клетки врага и ставит мины.
        var fixture = CreateFixture(selfTypes: [CardType.Trebuchet], opponentBoard: HalfOpenBoard(8));

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.Error.Should().BeEmpty();
        card.Cells.Should().NotBeEmpty();
        // Центр ромба 4 накрывает x-2..x+1: у клеток с x >= 6 в ромбе нет открытых клеток (x < 4).
        card.Cells.Should().NotContain(cell => cell.X >= 6);
    }

    [Fact]
    public void Trebuchet_FullyClosedOpponentBoard_HasNoCells()
    {
        var fixture = CreateFixture(selfTypes: [CardType.Trebuchet], opponentBoard: new TestBoardBuilder(5).Build());

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.Error.Should().BeEmpty();
        card.Cells.Should().BeEmpty();
    }

    [Fact]
    public void OpponentBomb_FlaggedCell_IsNotLegal()
    {
        var opponentBoard = new TestBoardBuilder(3).WithFlagAt((1, 1)).Build();
        var fixture = CreateFixture(selfTypes: [CardType.OpponentBomb], opponentBoard: opponentBoard);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.Error.Should().BeEmpty();
        card.Cells.Should().HaveCount(8);
        card.Cells.Should().NotContain(cell => cell.X == 1 && cell.Y == 1);
    }

    [Fact]
    public void MinefieldScout_LegalWhenEitherOrientationHitsClosedCells()
    {
        // Открыто всё, кроме строки y = 3. Размер линии в конфиге 4..5: от (0, 2) вертикаль
        // задевает (0, 3), горизонталь нет. От (0, 0) ни одна ориентация до строки 3 не дотягивается.
        var free = new List<(int x, int y)>();
        for (var y = 0; y < 7; y++)
        {
            for (var x = 0; x < 7; x++)
            {
                if (y != 3)
                    free.Add((x, y));
            }
        }

        var board = new TestBoardBuilder(7).WithFreeAt(free.ToArray()).Build();
        var fixture = CreateFixture(selfTypes: [CardType.MinefieldScout], selfBoard: board);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.Error.Should().BeEmpty();
        CardConfigs.MinefieldScout.Size.Should().BeInRange(4, 5, "the test geometry assumes this reach");
        card.Cells.Should().Contain(cell => cell.X == 0 && cell.Y == 2, "vertical line reaches the closed row");
        card.Cells.Should().NotContain(cell => cell.X == 0 && cell.Y == 0, "neither orientation reaches the closed row");
    }

    [Theory]
    [InlineData(CardType.Medic)]
    [InlineData(CardType.Overclock)]
    [InlineData(CardType.Siphon)]
    public void NoPositionCards_NeedNoPosition_AndNoError(CardType type)
    {
        var fixture = CreateFixture(selfTypes: [type]);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.NeedsPosition.Should().BeFalse();
        card.Cells.Should().BeEmpty();
        card.Error.Should().BeEmpty();
    }

    [Fact]
    public void Bloodhound_OwnBoardNotGenerated_EveryCellIsLegal()
    {
        var selfBoard = new Board(Guid.NewGuid(), Options.Create(new BoardOptions { Size = 5, Mines = 0 }));
        var fixture = CreateFixture(selfTypes: [CardType.Bloodhound], selfBoard: selfBoard);

        var card = Build(fixture).Cards.Should().ContainSingle().Subject;

        selfBoard.IsGenerated.Should().BeFalse();
        card.Error.Should().BeEmpty();
        card.NeedsPosition.Should().BeTrue();
        card.Cells.Should().HaveCount(25);
    }

    [Fact]
    public void ZipZap_OwnBoardNotGenerated_ReturnsError()
    {
        var selfBoard = new Board(Guid.NewGuid(), Options.Create(new BoardOptions { Size = 5, Mines = 0 }));
        var fixture = CreateFixture(selfTypes: [CardType.ZipZap], selfBoard: selfBoard);

        var card = Build(fixture).Cards.Should().ContainSingle().Subject;

        card.Error.Should().Be("Own board is not generated yet: open a cell first");
        card.Cells.Should().BeEmpty();
    }

    [Fact]
    public void NotEnoughMana_ReportsErrorWithEffectiveCost()
    {
        var fixture = CreateFixture(selfTypes: [CardType.Medic], opponentBoard: HalfOpenBoard(5));
        var self = fixture.Context.UserToPlayer.Values.Single(player => player.User.Id == fixture.SelfId);
        self.Mana.SetCurrent(new MoveSnapshot(), 1);

        var response = Build(fixture);
        var card = response.Cards.Should().ContainSingle().Subject;

        card.ManaCost.Should().Be(CardConfigs.All.All[CardType.Medic].ManaCost);
        card.Error.Should().Be("Not enough mana: 4 needed, 1 left");
    }

    [Fact]
    public void ZipZap_NeedsNoExtraCard()
    {
        var fixture = CreateFixture(selfTypes: [CardType.ZipZap, CardType.Medic], selfBoard: HalfOpenBoard(5));

        var card = Build(fixture).Cards.Should().Contain(c => c.Type == nameof(CardType.ZipZap)).Subject;

        card.NeedsExtraCard.Should().BeFalse();
        card.ExtraCardIds.Should().BeEmpty();
        card.NeedsPosition.Should().BeTrue();
    }

    private static IBoard HalfOpenBoard(int size)
    {
        var free = new List<(int x, int y)>();
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size / 2; x++)
                free.Add((x, y));
        }

        return new TestBoardBuilder(size).WithFreeAt(free.ToArray()).Build();
    }

    private static SharedAgentLegalPlaysResponse Build(Fixture fixture)
    {
        return AgentLegalPlaysBuilder.Build(
            fixture.Context,
            fixture.SelfId,
            fixture.SelfId,
            CardConfigs.All);
    }

    private static Fixture CreateFixture(CardType[] selfTypes, IBoard? selfBoard = null, IBoard? opponentBoard = null)
    {
        var selfId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var board = selfBoard ?? new TestBoardBuilder(3).WithOwner(selfId).Build();
        opponentBoard ??= new TestBoardBuilder(3).WithOwner(opponentId).Build();

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

        // Маны с запасом: тесты проверяют клетки и формы, а не бюджет.
        var mana = new Mana(modifiers);
        mana.SetMax(new MoveSnapshot(), 99);
        mana.SetCurrent(new MoveSnapshot(), 99);

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
