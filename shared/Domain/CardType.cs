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

        public static bool RequiresBoard(this CardType type)
        {
            return type switch
            {
                CardType.Trebuchet => true,
                CardType.Trebuchet_Max => true,
                CardType.Bloodhound => true,
                CardType.Bloodhound_Max => true,
                CardType.TrebuchetAimer => false,
                CardType.TrebuchetAimer_Max => false,
                CardType.ErosionDozer => true,
                CardType.ErosionDozer_Max => true,
                CardType.Gravedigger => false,
                CardType.ZipZap => true,
                CardType.ZipZap_Max => true,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}