using Shared;
using Scenario = Game.GamePlay.CardPreviews.CardPreviewScenarios.Scenario;

namespace Game.GamePlay.CardPreviews;

// Cell effect cards, captured with the effect applied (no round ticks).
public static class CardPreviewEffectScenarios
{
    public static readonly IReadOnlyDictionary<CardType, Scenario> All = new Dictionary<CardType, Scenario>
    {
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
    };
}
