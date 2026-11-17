namespace Shared
{
    /// <summary>
    /// Группа ачивок. Внутри одного типа ачивки различаются только тиром
    /// (например, <see cref="FlagsSet"/> — 10 / 100 / 1000 / 10000 / 100000 флагов).
    /// </summary>
    public enum InGameAchievementType
    {
        FlagsSet = 100,
        CellsOpened = 200,
        MinesDetonated = 300,
        CardsPlayed = 400,
        BuffsReceived = 500,
        EnemyCellsPlanted = 600,
        ManaSpent = 700,
        MatchesPlayed = 800,
        MatchesWon = 900,

        ScoutCardsPlayed = 1000,
        DefenseCardsPlayed = 1100,
        AttackCardsPlayed = 1200,
        BuffCardsPlayed = 1300,
        DebuffCardsPlayed = 1400,
    }
}
