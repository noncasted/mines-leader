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

        Lockdown = 2200,

        Sonar = 2400,

        Purge = 2500,

        Adrenaline = 2600,

        ManaSurge = 2700,

        BloodPact = 2800,

        CoinToss = 2900,

        ManaFountain = 3000,

        Focus = 3100,

        Shield = 3200,

        PowerSurge = 3300,

        Embargo = 3400,

        Recycler = 3500,

        MysticDraw = 3600,

        DoubleOrNothing = 3700,

        GamblersRuin = 3800,

        Excavator = 3900,
        Excavator_Max = 3910,

        ThermalVision = 4000,
        ThermalVision_Max = 4010,

        ChaosDiamond = 4100,

        ChaosScout = 4200,

        MineCluster = 4300,
        MineCluster_Max = 4310,

        CarpetBomb = 4400,
        CarpetBomb_Max = 4410,

        FortuneBlast = 4500,

        ChaosFog = 4600,

        Frost = 4700,
        Frost_Max = 4710,

        Blackout = 4800,
        Blackout_Max = 4810,

        FortuneCookie = 4900,

        Salvage = 5000,

        CardThief = 5100,

        SabotageDeck = 5200,
        Dud = 5210,

        SoulLink = 5300,

        MirrorMatch = 5400,

        DimensionRift = 5500,
    }

    public enum CardGroup
    {
        Scout = 100,
        Defense = 200,
        Attack = 300,
        Buff = 400,
        Debuff = 500,
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