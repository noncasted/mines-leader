using Cluster.Configs;
using Common.Reactive;
using Game.GamePlay.Snapshots;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay;

public class LastManStandingTurnBasedRound : Service, IGameRound
{
    public LastManStandingTurnBasedRound(
        IGameContext gameContext,
        IGameReadyAwaiter readyAwaiter,
        IPlayersReadyAwaiter playersReadyAwaiter,
        ISnapshotSender snapshotSender,
        ISnapshotDiffGuard diffGuard,
        IRoundActionService roundActionService,
        RoundPlayers players,
        IGameModeConfig modeOptions,
        IPlayerConfig playerConfig,
        IBotConfig botConfig,
        ILogger<LastManStandingTurnBasedRound> logger,
        ISessionLogger sessionLogger,
        IAgentObservationPublisher observationPublisher,
        MatchCreateOptions matchOptions) : base("game-round")
    {
        _gameContext = gameContext;
        _readyAwaiter = readyAwaiter;
        _playersReadyAwaiter = playersReadyAwaiter;
        _snapshotSender = snapshotSender;
        _diffGuard = diffGuard;
        _roundActionService = roundActionService;
        _players = players;
        _modeOptions = modeOptions;
        _playerConfig = playerConfig;
        _botConfig = botConfig;
        _logger = logger;
        _sessionLogger = sessionLogger;
        _observationPublisher = observationPublisher;
        _matchOptions = matchOptions;
    }

    private Guid _currentPlayerId;
    private int _currentRound;

    private readonly RoundPlayers _players;
    private readonly IGameContext _gameContext;
    private readonly IGameReadyAwaiter _readyAwaiter;
    private readonly IPlayersReadyAwaiter _playersReadyAwaiter;
    private readonly ISnapshotSender _snapshotSender;
    private readonly ISnapshotDiffGuard _diffGuard;
    private readonly IRoundActionService _roundActionService;
    private readonly IGameModeConfig _modeOptions;
    private readonly IPlayerConfig _playerConfig;
    private readonly IBotConfig _botConfig;
    private readonly ILogger<LastManStandingTurnBasedRound> _logger;
    private readonly ISessionLogger _sessionLogger;
    private readonly IAgentObservationPublisher _observationPublisher;
    private readonly MatchCreateOptions _matchOptions;

    private readonly ViewableProperty<IPlayer> _currentPlayer = new(null!);

    private ILifetime? _roundForcedLifetime;

    private LastManStandingTurnBasedModeOptions ModeOptions => _modeOptions.Value.LastManStandingTurnBased;

    public IViewableProperty<IPlayer> CurrentPlayer => _currentPlayer!;

    public async Task<Guid> Process(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Hand.SetSize(_playerConfig.Value.HandSize);
            player.Deck.Init(_playerConfig.Value.DeckSize);
        }

        ListenPlayersEvents(lifetime);

        await _readyAwaiter.Await(lifetime);

        var players = _gameContext.Players;

        var snapshot = new MoveSnapshot();

        var initPreState = _diffGuard.IsEnabled == true
            ? GameStateCapture.Capture(_gameContext)
            : null;

        foreach (var player in _gameContext.Players)
        {
            player.Health.SetMax(snapshot, ModeOptions.PlayerHealth);
            player.Health.SetCurrent(snapshot, ModeOptions.PlayerHealth);

            player.Mana.SetMax(snapshot, ModeOptions.PlayerStartMana);
            player.Mana.Restore(snapshot);

            player.Moves.SetMax(snapshot, GetMovesMax(player));
        }

        if (_matchOptions.Fixture != null)
            AgentMatchFixtureApplier.ApplyDecks(_gameContext, _matchOptions.Fixture, snapshot);

        foreach (var player in players)
        {
            if (AgentMatchFixtureApplier.ShouldSkipRestore(player, _matchOptions.Fixture))
                continue;

            _players.RestoreCards(player, snapshot);
        }

        if (_matchOptions.Fixture != null)
            AgentMatchFixtureApplier.Apply(_gameContext, _matchOptions.Fixture, snapshot);

        snapshot.RecordGameStarted(ModeOptions.CardMovesCost);
        snapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, secondsLeft: 0);

        if (initPreState != null)
        {
            var postState = GameStateCapture.Capture(_gameContext);
            _diffGuard.Validate(initPreState, snapshot.Collect(), postState, "round:init");
        }

        _snapshotSender.Send(snapshot);

        var playersReadyLifetime = lifetime.Child();

        foreach (var player in players)
            player.User.Lifetime.Listen(() => playersReadyLifetime.Terminate());

        await _playersReadyAwaiter.Await(playersReadyLifetime);

        var previousPlayer = ResolvePreviousPlayer(players);
        if (previousPlayer != null)
            _currentPlayer.Set(previousPlayer);

        var roundsCount = 0;

        while (IsGameOver() == false)
        {
            var nextPlayer = players.First(t => t != _currentPlayer.Value);
            roundsCount++;
            _sessionLogger.LogRoundStart(nextPlayer.User.Id, roundsCount);
            await ProcessRound(lifetime, nextPlayer);
            _currentRound++;
            EmitRoundSnapshot();
            _sessionLogger.LogRoundEnd(nextPlayer.User.Id, roundsCount);
        }

        var winner = GetWinner();
        _sessionLogger.LogGameOver(winner, GetWinReason());

        return winner;

        bool IsGameOver()
        {
            if (lifetime.IsTerminated == true)
                return true;

            if (players.Any(p => p.Health.Current == 0))
                return true;

            if (players.Any(p => p.User.Lifetime.IsTerminated == true))
                return true;

            if (roundsCount >= 2)
            {
                var flagWinner = _players.GetFlagWinner();

                if (flagWinner != Guid.Empty)
                    return true;
            }

            return false;
        }

        Guid GetWinner()
        {
            foreach (var player in players)
            {
                if (player.Health.Current <= 0)
                    return _gameContext.GetOpponent(player).User.Id;
            }

            foreach (var (user, player) in _gameContext.UserToPlayer)
            {
                if (user.Lifetime.IsTerminated == true)
                    return _gameContext.GetOpponent(player).User.Id;
            }

            var flagWinner = _players.GetFlagWinner();

            if (flagWinner != Guid.Empty)
                return flagWinner;

            return Guid.Empty;
        }

        string GetWinReason()
        {
            foreach (var player in players)
            {
                if (player.Health.Current <= 0)
                    return $"Player {player.User.Id} health reached 0";
            }

            foreach (var (user, _) in _gameContext.UserToPlayer)
            {
                if (user.Lifetime.IsTerminated == true)
                    return $"Player {user.Id} disconnected";
            }

            if (_players.GetFlagWinner() != Guid.Empty)
                return "All opponent mines flagged";

            if (lifetime.IsTerminated == true)
                return "Match lifetime terminated";

            return "Unknown";
        }
    }

    public void SkipTurn()
    {
        _sessionLogger.LogTurnSkipped(_currentPlayer.Value?.User.Id ?? Guid.Empty);
        _roundForcedLifetime!.Terminate();
    }

    private async Task ProcessRound(IReadOnlyLifetime lifetime, IPlayer player)
    {
        _roundForcedLifetime = lifetime.Child();
        var roundForcedLifetime = _roundForcedLifetime;

        _currentPlayerId = player.User.Id;

        {
            var startSnapshot = new MoveSnapshot();

            var startPreState = _diffGuard.IsEnabled == true
                ? GameStateCapture.Capture(_gameContext)
                : null;

            player.Moves.Restore(startSnapshot);
            startSnapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, secondsLeft: 0);

            if (startPreState != null)
            {
                var postState = GameStateCapture.Capture(_gameContext);
                _diffGuard.Validate(startPreState, startSnapshot.Collect(), postState, "round:start");
            }

            _snapshotSender.Send(startSnapshot);
        }

        _currentPlayer.Set(player);
        // opponent_turn must go out after Restore, otherwise EndTurn sees IsOwnTurn
        // with MovesLeft still locked at 0. turn_start follows for waiters.
        _observationPublisher.Publish(player.User.Id, "opponent_turn", false, string.Empty);
        _observationPublisher.Publish(player.User.Id, "turn_start", false, string.Empty);

        try
        {
            await TurnsCountdown();
        }
        catch (TaskCanceledException)
        {
            // Ignore
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in round timer");
        }

        var endSnapshot = new MoveSnapshot();

        var endPreState = _diffGuard.IsEnabled == true
            ? GameStateCapture.Capture(_gameContext)
            : null;

        if (player.Mana.ResultMax < ModeOptions.MaxManaCap)
        {
            player.Mana.SetMax(endSnapshot, player.Mana.ResultMax + 1);
        }

        player.Mana.Restore(endSnapshot);
        _sessionLogger.LogManaChanged(player.User.Id, player.Mana.Current, player.Mana.ResultMax);

        _players.RestoreCards(player, endSnapshot);

        _roundActionService.Tick(endSnapshot);
        player.Moves.Lock(endSnapshot);

        if (endPreState != null)
        {
            var postState = GameStateCapture.Capture(_gameContext);
            _diffGuard.Validate(endPreState, endSnapshot.Collect(), postState, "round:end");
        }

        _snapshotSender.Send(endSnapshot);

        roundForcedLifetime.Terminate();

        return;

        async Task TurnsCountdown()
        {
            var timeSpan = TimeSpan.FromSeconds(0.2);

            while (player.Moves.Left > 0 && _roundForcedLifetime.IsTerminated == false)
                await Task.Delay(timeSpan, _roundForcedLifetime.Token);
        }
    }

    private void EmitRoundSnapshot()
    {
        var snapshot = new MoveSnapshot();
        snapshot.RecordLastManStandingRound(_currentPlayerId, _currentRound, secondsLeft: 0);
        _snapshotSender.Send(snapshot);
    }

    /// <summary>
    /// The turn loop plays <c>First(p != current)</c>, so this sets the previous player.
    /// Fixture-null and HumanGoesFirst keep the bot as previous (human acts first).
    /// HumanGoesFirst=false sets the human as previous (bot acts first).
    /// </summary>
    private IPlayer? ResolvePreviousPlayer(IReadOnlyList<IPlayer> players)
    {
        var botPlayer = players.FirstOrDefault(p => p.User.IsBot);
        var humanPlayer = players.FirstOrDefault(p => p.User.IsBot == false);

        if (_matchOptions.Fixture != null && _matchOptions.Fixture.HumanGoesFirst == false)
            return humanPlayer;

        return botPlayer;
    }

    /// <summary>
    /// Бот может ходить чаще человека: скорость вскрытия поля упирается в ходы,
    /// и это единственная честная ручка сложности, не меняющая правила для игрока.
    /// </summary>
    private int GetMovesMax(IPlayer player)
    {
        if (player.User.IsBot == false)
            return ModeOptions.PlayerMoves;

        var botMoves = MatchBotProfile.ResolveConfig(_matchOptions, _botConfig).MovesPerRound;

        return botMoves > 0 ? botMoves : ModeOptions.PlayerMoves;
    }

    private void ListenPlayersEvents(IReadOnlyLifetime lifetime)
    {
        foreach (var player in _gameContext.Players)
        {
            player.Health.Updated.Advise(lifetime, () => {
                if (player.Health.Current > 0)
                    return;

                _roundForcedLifetime?.Terminate();
            });

            player.User.Lifetime.Listen(() => {
                SkipTurn();

                if (lifetime.IsTerminated == true || _roundForcedLifetime == null)
                    return;

                _roundForcedLifetime.Terminate();
            });
        }
    }
}
