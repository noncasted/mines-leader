using Shared;

namespace Game.GamePlay.CardPreviews;

/// <summary>
/// Pre-designed board layouts per card type for the menu preview feature.
/// Each scenario pairs a visual board layout (DSL — see <c>BoardLayoutParser</c>)
/// with a target position encoded as the <c>x</c> marker in the layout.
///
/// Field-modifying cards live here, including random-sized ones (Chaos*, FortuneBlast,
/// OpponentFlagReshuffle) — the preview uses a deterministic <c>PreviewGameRandom</c>
/// that always picks the max, so each scenario shows the card's full footprint.
/// Pure resource/buff/hand cards have no preview bundle.
/// </summary>
public static class CardPreviewScenarios
{
    /// <summary>
    /// <paramref name="MineAtTarget"/> forces the target cell to carry a mine after layout parsing
    /// (used by ChainReaction / OpponentBomb where the card requires a mine under the cursor).
    /// </summary>
    public sealed record Scenario(string Layout, bool MineAtTarget = false);

    private static readonly Dictionary<CardType, Scenario> _scenarios = Merge(
        CardPreviewScoutScenarios.All,
        CardPreviewCrossBoardScenarios.All,
        CardPreviewEffectScenarios.All,
        CardPreviewRandomScenarios.All);

    public static IReadOnlyDictionary<CardType, Scenario> All
    {
        get
        {
            if (_expanded != null)
                return _expanded;

            var expanded = new Dictionary<CardType, Scenario>(_scenarios);

            foreach (var (normalType, scenario) in _scenarios)
            {
                var maxName = normalType.ToString() + "_Max";

                if (Enum.TryParse<CardType>(maxName, out var maxType) && expanded.ContainsKey(maxType) == false)
                    expanded[maxType] = scenario;
            }

            _expanded = expanded;
            return _expanded;
        }
    }

    private static IReadOnlyDictionary<CardType, Scenario>? _expanded;

    private static Dictionary<CardType, Scenario> Merge(params IReadOnlyDictionary<CardType, Scenario>[] groups)
    {
        var scenarios = new Dictionary<CardType, Scenario>();

        foreach (var group in groups)
        {
            foreach (var (type, scenario) in group)
                scenarios[type] = scenario;
        }

        return scenarios;
    }
}