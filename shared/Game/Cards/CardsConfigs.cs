using System;

namespace Shared
{
    public static class CardsConfigs
    {
        public class Trebuchet
        {
            public const int NormalSize = 4;
            public const int MaxSize = 6;

            public const int NormalMines = 8;
            public const int MaxMines = 12;
        }

        public class Bloodhound
        {
            public const int NormalSize = 4;
            public const int MaxSize = 6;
        }

        public class ErosionDozer
        {
            public const int NormalSize = 5;
            public const int MaxSize = 7;
        }

        public class ZipZap
        {
            public const int NormalSize = 3;
            public const int MaxSize = 4;
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
    }
}