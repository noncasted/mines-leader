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

    private static readonly Dictionary<CardType, Scenario> _scenarios = new()
    {
        // --- Scout (own board) ---

        // ErosionDozer — reveals taken cells in a radius, detonating mines along the way.
        [CardType.ErosionDozer] = new Scenario("""
                                               t t t t m t t t
                                               t m t t t t t t
                                               t t t t t m t t
                                               t t t _ t t t t
                                               t t t x m t t t
                                               t t m t t t m t
                                               t t t t t t t t
                                               t t t m t t t t
                                               """),

        // Sonar — flags unflagged mines in a rhombus.
        [CardType.Sonar] = new Scenario("""
                                        t t t t t t t t
                                        t t t m t t t t
                                        t t t t t t t t
                                        t t m t t m t t
                                        t t t t x t t t
                                        t t t t t t t t
                                        t t m t t t t t
                                        t t t t t t t t
                                        """),

        // Excavator — cross pattern: flags mines, reveals safe cells.
        [CardType.Excavator] = new Scenario("""
                                            t t t t m t t t
                                            t t t t t t t t
                                            t t t t t t t t
                                            t t t t t t t t
                                            m t t t x t t m
                                            t t t t t t t t
                                            t t t t t t t t
                                            t t t t m t t t
                                            """),

        // MinefieldScout — line pattern: flags mines, reveals safe cells.
        [CardType.MinefieldScout] = new Scenario("""
                                                 t t t t t t t t
                                                 t t t t m t t t
                                                 t t t t t t t t
                                                 t t t t t t t t
                                                 m t t t x t t m
                                                 t t t t t t t t
                                                 t t t t m t t t
                                                 t t t t t t t t
                                                 """),

        // Bloodhound — detonates every mine in a rhombus and reveals the area.
        [CardType.Bloodhound] = new Scenario("""
                                             t t t t t t t t
                                             t t t m t t t t
                                             t t m t m t t t
                                             t t t t t t t t
                                             t t t t x t t t
                                             t t t m t t t t
                                             t t t t m t t t
                                             t t t t t t t t
                                             """),

        // ZipZap — chains through nearby mines. Requires free cells in the strike radius
        // plus a dense cluster of unflagged mines so the chain can jump several times.
        // Normal: ~3-mine chain east of the target.
        [CardType.ZipZap] = new Scenario("""
                                         t t t t t t t t
                                         t t t t t m t t
                                         t t t t m t t t
                                         t t _ _ _ _ _ t
                                         t _ x m m m m t
                                         t t _ _ _ _ _ t
                                         t t t t m t t t
                                         t t t t t t m t
                                         """),

        // Max: longer chain + more mines in range so the lightning visibly jumps 5+ times.
        [CardType.ZipZap_Max] = new Scenario("""
                                             t t t t t t t t
                                             t t t m t m t t
                                             t t m t m t m t
                                             t _ _ _ _ _ _ t
                                             t x m m m m m t
                                             t _ _ _ _ _ _ t
                                             t t m t m t m t
                                             t t t m t m t t
                                             """),

        // --- CrossBoard (opponent board) ---

        // MineCluster — plants mines in a cross on free cells.
        [CardType.MineCluster] = new Scenario("""
                                              _ _ _ _ _ _ _ _
                                              _ _ _ _ _ _ _ _
                                              _ _ _ _ _ _ _ _
                                              _ _ _ _ x _ _ _
                                              _ _ _ _ _ _ _ _
                                              _ _ _ _ _ _ _ _
                                              _ _ _ _ _ _ _ _
                                              _ _ _ _ _ _ _ _
                                              """),

        // CarpetBomb — plants mines along a line on free cells.
        [CardType.CarpetBomb] = new Scenario("""
                                             t t t t _ t t t
                                             t t t t _ t t t
                                             t t t t _ t t t
                                             t t t t _ t t t
                                             _ _ _ _ x _ _ _
                                             t t t t _ t t t
                                             t t t t _ t t t
                                             t t t t _ t t t
                                             """),

        // Trebuchet — fills a rhombus with mines on free cells.
        // Context mines at the edges make the untouched free cells around the target
        // show MinesAround numbers before the card runs, and updated numbers after.
        [CardType.Trebuchet] = new Scenario("""
                                            t t t m t m t t
                                            t _ _ _ _ _ _ t
                                            t _ _ _ _ _ _ t
                                            m _ _ _ _ _ _ m
                                            t _ _ _ x _ _ t
                                            m _ _ _ _ _ _ m
                                            t _ _ _ _ _ _ t
                                            t t m t m t t t
                                            """),

        // OpponentBomb — detonates a single cell. Force a mine at target for the visible explosion.
        [CardType.OpponentBomb] = new Scenario("""
                                               t t t t t t t t
                                               t t t t t t t t
                                               t t t t t t t t
                                               t t t t t t t t
                                               t t t t x t t t
                                               t t t t t t t t
                                               t t t t t t t t
                                               t t t t t t t t
                                               """, MineAtTarget: true),

        // OpponentFlagErase — clears flags inside a rhombus.
        [CardType.OpponentFlagErase] = new Scenario("""
                                                    t t t t t t t t
                                                    t t t f t t t t
                                                    t t f t f t t t
                                                    t t t t t t t t
                                                    t t t f x f t t
                                                    t t t t t t t t
                                                    t t f t f t t t
                                                    t t t f t t t t
                                                    """),

        // ChainReaction — target must be a mine; chains to nearby mines and spawns new ones
        // in a rhombus on free cells around each target. Shown on a nearly empty field so
        // the newly spawned mines are obvious against the background.
        [CardType.ChainReaction] = new Scenario("""
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ m _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ x _ _ _ _
                                                _ _ _ _ _ _ _ _
                                                _ _ _ _ _ _ m _
                                                _ _ _ _ _ _ _ _
                                                """, MineAtTarget: true),

        // --- Effects (no round ticks — captured with effect applied) ---

        // Smoke — covers a rhombus with smoke effect.
        [CardType.Smoke] = new Scenario("""
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t x t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        """),

        // Blackout — hides mine numbers in a rhombus.
        [CardType.Blackout] = new Scenario("""
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t x t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           """),

        // Frost — freezes a rhombus of cells.
        [CardType.Frost] = new Scenario("""
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t x t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        t t t t t t t t
                                        """),

        // FogOfWar — fogs free cells in a rhombus.
        // Mines scattered through the middle (not only edges) so every fogged free cell has
        // a real MinesAround number — the overlay then visibly hides them, which is the
        // whole point of the card.
        [CardType.FogOfWar] = new Scenario("""
                                           t m t m t m t t
                                           _ _ _ _ _ _ _ t
                                           _ _ m _ m _ _ t
                                           _ _ _ _ _ _ _ t
                                           _ _ _ _ x _ m t
                                           _ _ _ _ _ _ _ t
                                           _ _ m _ m _ _ t
                                           t m t m t m t t
                                           """),

        // ThermalVision — highlights mines inside a rhombus.
        [CardType.ThermalVision] = new Scenario("""
                                                t t t t t t t t
                                                t t t m t t t t
                                                t t m t m t t t
                                                t t t t t t t t
                                                t t t m x t m t
                                                t t t t t t t t
                                                t t t m t m t t
                                                t t t t m t t t
                                                """),

        // --- Random-sized cards (PreviewGameRandom returns max) ---

        // ChaosDiamond — random-size rhombus: flags mines, reveals safe cells on own board.
        [CardType.ChaosDiamond] = new Scenario("""
                                               t t t t m t t t
                                               t t t m t m t t
                                               t t m t t t m t
                                               t m t t t t t m
                                               t t t t x t t t
                                               t m t m t t t t
                                               t t m t m t t t
                                               t t t m t m t t
                                               """),

        // ChaosScout — random-length line: flags mines, reveals safe cells on own board.
        [CardType.ChaosScout] = new Scenario("""
                                             t t t t t t t t
                                             t t t t t t t t
                                             t t t t m t t t
                                             t t t t t t t t
                                             m t t t x t t m
                                             t t t t t t t t
                                             t t t t m t t t
                                             t t t t t t t t
                                             """),

        // FortuneBlast — random-size rhombus of mines on free cells (opponent).
        [CardType.FortuneBlast] = new Scenario("""
                                               _ _ _ _ _ _ _ _
                                               _ _ _ _ _ _ _ _
                                               _ _ _ _ _ _ _ _
                                               _ _ _ _ _ _ _ _
                                               _ _ _ _ x _ _ _
                                               _ _ _ _ _ _ _ _
                                               _ _ _ _ _ _ _ _
                                               _ _ _ _ _ _ _ _
                                               """),

        // ChaosFog — random-size rhombus smoke-effect on opponent.
        [CardType.ChaosFog] = new Scenario("""
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t x t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           t t t t t t t t
                                           """),

        // OpponentFlagReshuffle — swaps flagged/unflagged cells inside a rhombus on opponent.
        [CardType.OpponentFlagReshuffle] = new Scenario("""
                                                        t t t t t t t t
                                                        t t t f t t t t
                                                        t t t t t t t t
                                                        t t f t f t t t
                                                        t t t t x t t t
                                                        t t t t t t t t
                                                        t t t t f t t t
                                                        t t t t t t t t
                                                        """)
    };

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
}