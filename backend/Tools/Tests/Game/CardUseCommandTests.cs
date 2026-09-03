using Cluster.Configs;
using Common.Reactive;
using FluentAssertions;
using Game.GamePlay;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Атакующие карты (Target = OpponentBoard) запрещены, пока противник не сделал первый ход:
/// его доска ещё не сгенерирована, и карта создала бы поле за него.
/// </summary>
public class CardUseCommandTests : PlayerCardTestsBase
{
    [Fact]
    public void Execute_OpponentBoardCardWithNotGeneratedBoard_Fails()
    {
        var (command, context, card) = Create(CardType.OpponentBomb, isOpponentBoardGenerated: false);

        var response = command.Execute(context, new SharedGameAction.CardUse
        {
            CardId = card.Id,
            Payload = new CardUsePayload.OpponentBomb { Type = card.Type }
        });

        response.HasError.Should().BeTrue();
        response.Message.Should().Be("Opponent board is not generated yet");
        context.Player.Hand.DidNotReceive().Remove(Arg.Any<Guid>());
        context.Player.Mana.DidNotReceive().Use(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
    }

    [Fact]
    public void Execute_OpponentBoardCardWithGeneratedBoard_PassesTheGuard()
    {
        var (command, context, card) = Create(CardType.OpponentBomb, isOpponentBoardGenerated: true);

        var request = new SharedGameAction.CardUse
        {
            CardId = card.Id,
            Payload = new CardUsePayload.OpponentBomb { Type = card.Type }
        };

        // Гард пропускает карту дальше, а розыгрыш падает уже на пустом провайдере карт.
        var act = () => command.Execute(context, request);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Execute_NotEnoughMana_FailsBeforeCardUse()
    {
        var (command, context, card) = Create(CardType.Medic, isOpponentBoardGenerated: true);
        context.Player.Mana.Current.Returns(1);

        var response = command.Execute(context, new SharedGameAction.CardUse
        {
            CardId = card.Id,
            Payload = new CardUsePayload.Medic { Type = card.Type }
        });

        response.HasError.Should().BeTrue();
        response.Message.Should().Be("Not enough mana: 4 needed, 1 left");
        context.Player.Hand.DidNotReceive().Remove(Arg.Any<Guid>());
        context.Player.Mana.DidNotReceive().Use(Arg.Any<MoveSnapshot>(), Arg.Any<int>());
        context.Player.Modifiers.DidNotReceive().Reset(Arg.Any<MoveSnapshot>(), Arg.Any<PlayerModifier>());
    }

    [Fact]
    public void Execute_FocusDiscount_CountsTowardsMana()
    {
        var (command, context, card) = Create(CardType.Medic, isOpponentBoardGenerated: true);
        context.Player.Mana.Current.Returns(3);
        context.Player.Modifiers.Get(PlayerModifier.NextCardDiscount).Returns(1f);

        // Гард пропускает (4 - 1 = 3 <= 3), розыгрыш падает уже на пустом провайдере карт.
        var act = () => command.Execute(context, new SharedGameAction.CardUse
        {
            CardId = card.Id,
            Payload = new CardUsePayload.Medic { Type = card.Type }
        });

        act.Should().Throw<Exception>();
    }

    private static (TestableCardUseCommand Command, GameCommand<SharedGameAction.CardUse>.Context Context, ActiveCard Card)
        Create(CardType type, bool isOpponentBoardGenerated)
    {
        var player = MockPlayer();
        player.Mana.Current.Returns(10);
        var opponent = MockPlayer();
        opponent.Board.IsGenerated.Returns(isOpponentBoardGenerated);

        var card = new ActiveCard { Id = Guid.NewGuid(), Type = type };
        player.Hand.Entries.Returns(new List<ActiveCard> { card });

        var gameContext = MockGameContext(player, opponent);

        var utils = new GameCommandUtils(
            gameContext, RoundOf(player), Substitute.For<IServiceProvider>(),
            Substitute.For<ISnapshotSender>(), Substitute.For<ISnapshotDiffGuard>(),
            Substitute.For<ILogger<GameCommandUtils>>(), Substitute.For<ISessionLogger>(),
            new MatchStatsTracker());

        var modeConfig = Substitute.For<IGameModeConfig>();
        modeConfig.Value.Returns(new GameModeOptions());

        var command = new TestableCardUseCommand(
            utils,
            MockConfigs(),
            modeConfig,
            new MatchCreateOptions { Type = GameMatchType.TimeLimited });

        var context = new GameCommand<SharedGameAction.CardUse>.Context
        {
            Player = player,
            Lifetime = new Lifetime(),
            Snapshot = new MoveSnapshot()
        };

        return (command, context, card);
    }

    private class TestableCardUseCommand(
        GameCommandUtils utils,
        ICardConfigs configs,
        IGameModeConfig modeConfigs,
        MatchCreateOptions matchOptions) : CardUseCommand(utils, configs, modeConfigs, matchOptions)
    {
        public new EmptyResponse Execute(
            GameCommand<SharedGameAction.CardUse>.Context context,
            SharedGameAction.CardUse request) => base.Execute(context, request);
    }

    private static IGameRound RoundOf(IPlayer player)
    {
        var round = Substitute.For<IGameRound>();
        round.CurrentPlayer.Value.Returns(player);
        return round;
    }
}
