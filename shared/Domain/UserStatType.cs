using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    /// <summary>
    /// Counters accumulated during a match and summed up in the player grain after it ends.
    /// </summary>
    public enum UserStatType
    {
        FlagsSet = 100,
        FlagsRemoved = 110,

        CellsOpened = 200,
        MinesDetonated = 210,

        CardsPlayed = 300,
        CrossBoardCardsPlayed = 310,
        EnemyCellsPlanted = 320,

        BuffsReceived = 400,
        DebuffsReceived = 410,

        ManaSpent = 500,
        DamageTaken = 510,
        TurnsSkipped = 520,

        MatchesPlayed = 600,
        MatchesWon = 610,
        MatchesLost = 620,
    }

#if UNITY_5_3_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class UserStatTypeExtensions
    {
        static UserStatTypeExtensions()
        {
            All = Enum.GetValues(typeof(UserStatType)).Cast<UserStatType>().ToList();
        }

        public static readonly IReadOnlyList<UserStatType> All;
    }
}
