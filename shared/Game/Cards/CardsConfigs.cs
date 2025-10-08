using System;

namespace Shared
{
    public static class CardsConfigs
    {
        public class Bloodhound
        {
            public const int NormalSize = 4;
            public const int NormalMana = 2;
            public const int MaxSize = 6;
            public const int MaxMana = 4;
        }
        
        public class ErosionDozer
        {
            public const int NormalSize = 5;
            public const int NormalMana = 4;
            public const int MaxSize = 7;
            public const int MaxMana = 5;
        }
        
        public class Gravedigger
        {
            public const int Mana = 4;
        }
        
        public class Trebuchet
        {
            public const int NormalSize = 4;
            public const int NormalMana = 3;
            public const int MaxSize = 6;
            public const int MaxMana = 5;
        }
        
        public class TrebuchetAimer
        {
            public const int NormalMana = 2;
            public const int MaxMana = 4;
        }

        public class ZipZap
        {
            public const int NormalSize = 3;
            public const int NormalMana = 3;
            public const int MaxSize = 4;
            public const int MaxMana = 5;
            public const int SearchRadius = 6;
        }

        public static int GetSize(this CardType type)
        {
            return type switch
            {
                CardType.Trebuchet => Trebuchet.NormalSize,
                CardType.Trebuchet_Max => Trebuchet.MaxSize,
                CardType.Bloodhound => Bloodhound.NormalSize,
                CardType.Bloodhound_Max => Bloodhound.MaxSize,
                CardType.ErosionDozer => ErosionDozer.NormalSize,
                CardType.ErosionDozer_Max => ErosionDozer.MaxSize,
                CardType.ZipZap => ZipZap.NormalSize,
                CardType.ZipZap_Max => ZipZap.MaxSize,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }

        public static int GetManaCost(this CardType type)
        {
            return type switch
            {
                CardType.Trebuchet => Trebuchet.NormalMana,
                CardType.Trebuchet_Max => Trebuchet.MaxMana,
                CardType.Bloodhound => Bloodhound.NormalMana,
                CardType.Bloodhound_Max => Bloodhound.MaxMana,
                CardType.TrebuchetAimer => TrebuchetAimer.NormalMana,
                CardType.TrebuchetAimer_Max => TrebuchetAimer.MaxMana,
                CardType.ErosionDozer => ErosionDozer.NormalMana,
                CardType.ErosionDozer_Max => ErosionDozer.MaxMana,
                CardType.Gravedigger => Gravedigger.Mana,
                CardType.ZipZap => ZipZap.NormalMana,
                CardType.ZipZap_Max => ZipZap.MaxMana,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}