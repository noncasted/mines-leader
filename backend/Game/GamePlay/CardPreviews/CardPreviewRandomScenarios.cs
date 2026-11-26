using Shared;
using Scenario = Game.GamePlay.CardPreviews.CardPreviewScenarios.Scenario;

namespace Game.GamePlay.CardPreviews;

// Random-sized cards: PreviewGameRandom returns the max.
public static class CardPreviewRandomScenarios
{
    public static readonly IReadOnlyDictionary<CardType, Scenario> All = new Dictionary<CardType, Scenario>
    {
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
                                                        """),
    };
}
