using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    public enum PlayerModifier
    {
        TrebuchetBoost = 0,
        AdditionalMana = 1,
        AdditionalMoves = 2,
        AdditionalHealth = 3,
        NextCardDiscount = 4,
        AllCardsDiscount = 5,
        ManaCostPenalty = 6,
        Shield = 7,
        SoulLink = 8,
        BaseHealth = 9,
        BaseMoves = 10,
        BaseMana = 11,
    }

    public static class PlayerModifierExtensions
    {
        static PlayerModifierExtensions()
        {
            All = Enum.GetValues(typeof(PlayerModifier)).Cast<PlayerModifier>().ToList();
        }

        public static readonly List<PlayerModifier> All;
    }
}