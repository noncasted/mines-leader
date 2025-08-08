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
            All = Enum.GetValues<CardType>().ToList();
        }

        public static readonly IReadOnlyList<CardType> All;
    }
}