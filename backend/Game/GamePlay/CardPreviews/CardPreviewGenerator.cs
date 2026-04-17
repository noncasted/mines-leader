using Cluster.Configs;
using Common.Reactive;
using Game.GamePlay.Boards;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay.CardPreviews;

public interface ICardPreviewGenerator
{
    /// <summary>
    /// Returns pre-generated preview bundles. Generation runs lazily on the first call
    /// once <see cref="ICardConfigs"/> exposes a non-null <c>Value</c>.
    /// </summary>
    Task<IReadOnlyList<CardPreviewBundle>> GetBundlesAsync();
}

/// <summary>
/// Generates card preview bundles once at startup and caches them in-memory.
/// Each scenario: parse layout -> instantiate card with real <see cref="ICardConfigs"/> ->
/// drive card.Use() with a production-safe <see cref="PreviewPlayer"/> stub ->
/// collect <see cref="ICardActionData"/> from the resulting <see cref="MoveSnapshot"/>.
/// </summary>
public sealed class CardPreviewGenerator : ICardPreviewGenerator
{
    public CardPreviewGenerator(ICardConfigs configs, ILogger<CardPreviewGenerator> logger)
    {
        _configs = configs;
        _logger = logger;
    }

    private readonly ICardConfigs _configs;
    private readonly ILogger<CardPreviewGenerator> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<CardPreviewBundle>? _cached;

    public async Task<IReadOnlyList<CardPreviewBundle>> GetBundlesAsync()
    {
        if (_cached != null)
            return _cached;

        await _lock.WaitAsync();

        try
        {
            if (_cached != null)
                return _cached;

            _cached = Generate();
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    private IReadOnlyList<CardPreviewBundle> Generate()
    {
        if (_configs.Value == null)
        {
            _logger.LogWarning("[CardPreview] ICardConfigs.Value is null — skipping preview generation");
            return Array.Empty<CardPreviewBundle>();
        }

        var bundles = new List<CardPreviewBundle>();

        foreach (var (cardType, scenario) in CardPreviewScenarios.All)
        {
            try
            {
                var bundle = BuildBundle(cardType, scenario);

                if (bundle != null)
                    bundles.Add(bundle);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CardPreview] Failed to generate preview for {CardType}", cardType);
            }
        }

        _logger.LogInformation("[CardPreview] Generated {Count} card preview bundles", bundles.Count);
        return bundles;
    }

    private CardPreviewBundle? BuildBundle(CardType cardType, CardPreviewScenarios.Scenario scenario)
    {
        // All preview boards use Guid.Empty as OwnerId so every CardActionSnapshot.TargetPlayer
        // emitted into the bundle matches the single stub player on the client (MenuPreviewGameContext).
        var (board, target) = BoardLayoutParser.Parse(scenario.Layout, Guid.Empty);

        if (target.x < 0 || target.y < 0)
            throw new InvalidOperationException($"Scenario for {cardType} has no 'x' target marker");

        if (scenario.MineAtTarget
            && board.Cells.TryGetValue(target, out var targetCell)
            && targetCell is ITakenCell taken
            && taken.HasMine == false)
        {
            taken.SetMine();
            board.MinesScanner.Recalculate();
        }

        var initialState = BoardLayoutParser.Capture(board);
        var boardForCapture = board;
        var player = new PreviewPlayer(board);
        var snapshot = new MoveSnapshot();

        var context = new CardUseContext
        {
            Invoker = player,
            Snapshot = snapshot,
            CardId = Guid.NewGuid()
        };

        var result = RunCard(cardType, target, player, context);

        if (result.Result.HasError)
        {
            _logger.LogWarning("[CardPreview] Card {CardType} returned error: {Message}",
                cardType, result.Result.Message);
            return null;
        }

        var actions = snapshot.Collect().Records
                              .OfType<PlayerSnapshotRecord.CardUse>()
                              .Select(r => r.Data)
                              .ToList();

        var finalState = BoardLayoutParser.Capture(boardForCapture);

        return new CardPreviewBundle
        {
            CardType = cardType,
            Target = target,
            InitialState = initialState,
            FinalState = finalState,
            Actions = actions
        };
    }

    private CardUseResult RunCard(CardType cardType, Position target, PreviewPlayer invoker, CardUseContext context)
    {
        switch (cardType)
        {
            // --- Scout (own board) ---

            case CardType.ErosionDozer:
            case CardType.ErosionDozer_Max:
                return new ErosionDozer(_configs).Use(context, new CardUsePayload.ErosionDozer
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.Sonar:
                return new Sonar(_configs).Use(context, new CardUsePayload.Sonar
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.Excavator:
            case CardType.Excavator_Max:
                return new Excavator(_configs).Use(context, new CardUsePayload.Excavator
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.MinefieldScout:
            case CardType.MinefieldScout_Max:
                return new MinefieldScout(_configs).Use(context, new CardUsePayload.MinefieldScout
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.Bloodhound:
            case CardType.Bloodhound_Max:
                return new Bloodhound(_configs).Use(context, new CardUsePayload.Bloodhound
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.ZipZap:
            case CardType.ZipZap_Max:
                return new ZipZap(_configs).Use(context, new CardUsePayload.ZipZap
                {
                    Type = cardType,
                    Position = target
                });

            // --- Effect scout (uses round-action service to schedule dispose) ---

            case CardType.ThermalVision:
            case CardType.ThermalVision_Max:
                return new ThermalVision(_configs, new PreviewRoundActionService())
                    .Use(context, new CardUsePayload.ThermalVision
                    {
                        Type = cardType,
                        Position = target
                    });

            // --- CrossBoard (opponent = the parsed board; self gets a tiny throwaway board) ---

            case CardType.MineCluster:
            case CardType.MineCluster_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new MineCluster(_configs, ctx).Use(payload.Context,
                        new CardUsePayload.MineCluster { Type = cardType, Position = target }));

            case CardType.CarpetBomb:
            case CardType.CarpetBomb_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new CarpetBomb(_configs, ctx).Use(payload.Context,
                        new CardUsePayload.CarpetBomb { Type = cardType, Position = target }));

            case CardType.Trebuchet:
            case CardType.Trebuchet_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new Trebuchet(_configs, ctx).Use(payload.Context,
                        new CardUsePayload.Trebuchet { Type = cardType, Position = target }));

            case CardType.OpponentBomb:
                return RunCrossBoard(target, context, (ctx, _) =>
                    new OpponentBomb(ctx).Use(_.Context,
                        new CardUsePayload.OpponentBomb { Type = cardType, Position = target }));

            case CardType.OpponentFlagErase:
            case CardType.OpponentFlagErase_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new OpponentFlagErase(_configs, ctx).Use(payload.Context,
                        new CardUsePayload.OpponentFlagErase { Type = cardType, Position = target }));

            case CardType.ChainReaction:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new ChainReaction(_configs, ctx).Use(payload.Context,
                        new CardUsePayload.ChainReaction { Type = cardType, Position = target }));

            case CardType.Smoke:
            case CardType.Smoke_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new Smoke(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.Smoke { Type = cardType, Position = target }));

            case CardType.Blackout:
            case CardType.Blackout_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new Blackout(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.Blackout { Type = cardType, Position = target }));

            case CardType.Frost:
            case CardType.Frost_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new Frost(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.Frost { Type = cardType, Position = target }));

            case CardType.FogOfWar:
            case CardType.FogOfWar_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new FogOfWar(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.FogOfWar { Type = cardType, Position = target }));

            // --- Random-sized field cards (PreviewGameRandom returns max bound) ---

            case CardType.ChaosDiamond:
                return new ChaosDiamond(_configs, new PreviewGameRandom())
                    .Use(context, new CardUsePayload.ChaosDiamond { Type = cardType, Position = target });

            case CardType.ChaosScout:
                return new ChaosScout(_configs, new PreviewGameRandom())
                    .Use(context, new CardUsePayload.ChaosScout { Type = cardType, Position = target });

            case CardType.FortuneBlast:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new FortuneBlast(_configs, ctx, new PreviewGameRandom()).Use(payload.Context,
                        new CardUsePayload.FortuneBlast { Type = cardType, Position = target }));

            case CardType.ChaosFog:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new ChaosFog(_configs, new PreviewRoundActionService(), ctx, new PreviewGameRandom())
                        .Use(payload.Context,
                            new CardUsePayload.ChaosFog { Type = cardType, Position = target }));

            case CardType.OpponentFlagReshuffle:
            case CardType.OpponentFlagReshuffle_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new OpponentFlagReshuffle(_configs, ctx, new PreviewGameRandom())
                        .Use(payload.Context,
                            new CardUsePayload.OpponentFlagReshuffle { Type = cardType, Position = target }));

            default:
                throw new NotSupportedException(
                    $"CardPreviewGenerator does not yet support {cardType}. " +
                    "Add a handler in CardPreviewGenerator.RunCard.");
        }
    }

    /// <summary>
    /// Runs a cross-board card where the parsed scenario board is the opponent's board.
    /// Self gets a minimal throwaway board so IPlayer.Board isn't null.
    /// The <paramref name="run"/> callback receives a fresh <see cref="IGameContext"/>
    /// and a <see cref="CrossBoardInvoke"/> wrapper carrying the self-invoker <see cref="CardUseContext"/>.
    /// </summary>
    private CardUseResult RunCrossBoard(Position target, CardUseContext originalContext,
        Func<IGameContext, CrossBoardInvoke, CardUseResult> run)
    {
        var opponentPlayer = (PreviewPlayer)originalContext.Invoker;

        var (selfBoard, _) = BoardLayoutParser.Parse("""
            t t
            t t
            """, Guid.Empty);
        var selfInvoker = new PreviewPlayer(selfBoard);

        var gameContext = new PreviewGameContext(selfInvoker, opponentPlayer);
        var selfUseContext = new CardUseContext
        {
            Invoker = selfInvoker,
            Snapshot = originalContext.Snapshot,
            CardId = originalContext.CardId
        };

        return run(gameContext, new CrossBoardInvoke(selfUseContext, target));
    }

    private readonly record struct CrossBoardInvoke(CardUseContext Context, Position Target);

    private sealed class PreviewGameContext : IGameContext
    {
        public PreviewGameContext(IPlayer self, IPlayer opponent)
        {
            _players = new List<IPlayer> { self, opponent };
            _boards = new Dictionary<IPlayer, IBoard>
            {
                [self] = self.Board,
                [opponent] = opponent.Board
            };
            _userToPlayer = new Dictionary<IUser, IPlayer>
            {
                [self.User] = self,
                [opponent.User] = opponent
            };
        }

        private readonly List<IPlayer> _players;
        private readonly Dictionary<IPlayer, IBoard> _boards;
        private readonly Dictionary<IUser, IPlayer> _userToPlayer;
        private readonly ViewableDelegate _gameStarted = new();

        public IReadOnlyList<IPlayer> Players => _players;
        public IReadOnlyDictionary<IPlayer, IBoard> Boards => _boards;
        public IReadOnlyDictionary<IUser, IPlayer> UserToPlayer => _userToPlayer;
        public IViewableDelegate GameStarted => _gameStarted;

        public void AddPlayer(IPlayer player) => throw new NotSupportedException();
        public void OnGameStarted() => throw new NotSupportedException();
    }
}
