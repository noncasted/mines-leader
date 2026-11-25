using Shared;
using Scenario = Game.GamePlay.CardPreviews.CardPreviewScenarios.Scenario;

namespace Game.GamePlay.CardPreviews;

// Cross-board cards on the opponent board.
public static class CardPreviewCrossBoardScenarios
{
    public static readonly IReadOnlyDictionary<CardType, Scenario> All = new Dictionary<CardType, Scenario>
    {
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
        // Layout: mixed open/closed field so the explosion is visible against a realistic board.
        [CardType.OpponentBomb] = new Scenario("""
                                               _ _ _ _ _ _ _ _
                                               _ t t t t t t _
                                               _ t m t t m t _
                                               _ t t t t t t _
                                               _ t t t x t t _
                                               _ t t t t t t _
                                               _ t m t t m t _
                                               _ _ _ _ _ _ _ _
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
    };
}
