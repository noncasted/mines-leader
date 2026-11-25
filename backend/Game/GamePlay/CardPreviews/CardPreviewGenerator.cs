using Cluster.Configs;
using Infrastructure;
using Internal;
using Game.GamePlay.Boards;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay.CardPreviews;

public interface ICardPreviewGenerator
{
    /// <summary>
    /// The preview bundles of the current card configs. Generated in the local setup of the gateway, before it accepts a
    /// socket, and again on every change of the configs: a connection never pays for the generation.
    /// </summary>
    IReadOnlyList<CardPreviewBundle> Bundles { get; }
}

/// <summary>
/// Generates card preview bundles and keeps them in memory.
/// Each scenario: parse layout -> instantiate card with real <see cref="ICardConfigs"/> ->
/// drive card.Use() with a production-safe <see cref="PreviewPlayer"/> stub ->
/// collect <see cref="ICardActionData"/> from the resulting <see cref="MoveSnapshot"/>.
/// </summary>
public sealed class CardPreviewGenerator : ICardPreviewGenerator, ILocalSetupCompleted
{
    public CardPreviewGenerator(ICardConfigs configs, ILogger<CardPreviewGenerator> logger)
    {
        _configs = configs;
        _runner = new CardPreviewRunner(configs);
        _logger = logger;
    }

    private readonly ICardConfigs _configs;
    private readonly CardPreviewRunner _runner;
    private readonly ILogger<CardPreviewGenerator> _logger;
    private IReadOnlyList<CardPreviewBundle>? _bundles;

    public IReadOnlyList<CardPreviewBundle> Bundles =>
        _bundles ?? throw new InvalidOperationException("Card previews are read before the local setup generated them");

    // The configs are loaded on the Orleans start, before the local setup; the gateway takes sockets only after it.
    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _configs.View(lifetime, _ => _bundles = Generate());
        return Task.CompletedTask;
    }

    private IReadOnlyList<CardPreviewBundle> Generate()
    {
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

        if (scenario.MineAtTarget &&
            board.Cells.TryGetValue(target, out var targetCell) &&
            targetCell is ITakenCell taken &&
            taken.HasMine == false)
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

        var result = _runner.RunCard(cardType, target, player, context);

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
}
