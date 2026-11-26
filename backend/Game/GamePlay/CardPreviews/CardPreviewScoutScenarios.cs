using Shared;
using Scenario = Game.GamePlay.CardPreviews.CardPreviewScenarios.Scenario;

namespace Game.GamePlay.CardPreviews;

// Scout cards on the own board.
public static class CardPreviewScoutScenarios
{
    public static readonly IReadOnlyDictionary<CardType, Scenario> All = new Dictionary<CardType, Scenario>
    {
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
    };
}
