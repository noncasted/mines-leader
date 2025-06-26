using System;
using System.Collections.Generic;
using System.Linq;

namespace GamePlay.Players
{
    public enum PlayerModifier
    {
        TrebuchetBoost = 0, 
    }
    
    public static class PlayerModifierExtensions
    {
        static PlayerModifierExtensions()
        {
            All = Enum.GetValues(typeof(PlayerModifier)).Cast<PlayerModifier>().ToList();
        }

        public static readonly List<PlayerModifier> All = new();
    }
}