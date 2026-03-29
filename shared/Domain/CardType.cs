using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    public enum CardType
    {
        Trebuchet = 100,
        Trebuchet_Max = 110,

        Bloodhound = 200,
        Bloodhound_Max = 210,

        TrebuchetAimer = 300,
        TrebuchetAimer_Max = 310,

        ErosionDozer = 400,
        ErosionDozer_Max = 410,

        Gravedigger = 500,

        ZipZap = 600,
        ZipZap_Max = 610,

        OpponentBomb = 700,

        OpponentFlagErase = 800,
        OpponentFlagErase_Max = 810,

        OpponentFlagReshuffle = 900,
        OpponentFlagReshuffle_Max = 910,

        Smoke = 1000,
        Smoke_Max = 1010,

        Medic = 1100,

        MinefieldScout = 1200,
        MinefieldScout_Max = 1210,

        Siphon = 1400,

        ChainReaction = 1700,

        Overclock = 1800,

        FogOfWar = 1900,
        FogOfWar_Max = 1910,

        Scavenger = 2000,

        HandScramble = 2100,
    }

    public enum CardTarget
    {
        OwnBoard,
        OpponentBoard,
        Self,
        Opponent,
    }

    public static class CardTypeExtensions
    {
        static CardTypeExtensions()
        {
            All = Enum.GetValues(typeof(CardType)).Cast<CardType>().ToList();
        }

        public static readonly IReadOnlyList<CardType> All;
    }
}