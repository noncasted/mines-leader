using System.Collections.Generic;

namespace Shared
{
    public interface ICardConfig
    {
        CardType Type { get; }
        int Size { get; }
        int ManaCost { get; }
        CardTarget Target { get; }
    }

    public static class CardsConfigs
    {
        public class Bloodhound : ICardConfig
        {
            public CardType Type => CardType.Bloodhound;
            public int Size => 4;
            public int ManaCost => 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        public class Bloodhound_Max : ICardConfig
        {
            public CardType Type => CardType.Bloodhound_Max;
            public int Size => 4;
            public int ManaCost => 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        public class Trebuchet : ICardConfig
        {
            public CardType Type => CardType.Trebuchet;
            public int Size => 4;
            public int ManaCost => 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        public class Trebuchet_Max : ICardConfig
        {
            public CardType Type => CardType.Trebuchet_Max;
            public int Size => 6;
            public int ManaCost => 5;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        public class TrebuchetAimer : ICardConfig
        {
            public CardType Type => CardType.TrebuchetAimer;
            public int Size => 0;
            public int ManaCost => 2;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        public class TrebuchetAimer_Max : ICardConfig
        {
            public CardType Type => CardType.TrebuchetAimer_Max;
            public int Size => 0;
            public int ManaCost => 4;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        public class ErosionDozer : ICardConfig
        {
            public CardType Type => CardType.ErosionDozer;
            public int Size => 5;
            public int ManaCost => 4;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        public class ErosionDozer_Max : ICardConfig
        {
            public CardType Type => CardType.ErosionDozer_Max;
            public int Size => 7;
            public int ManaCost => 5;
            public CardTarget Target => CardTarget.OwnBoard;
        }

        public class Gravedigger : ICardConfig
        {
            public CardType Type => CardType.Gravedigger;
            public int Size => 0;
            public int ManaCost => 4;
            public CardTarget Target => CardTarget.Self;
        }

        public interface IZipZap : ICardConfig
        {
            int SearchRadius { get; }
        }

        public class ZipZap : IZipZap
        {
            public CardType Type => CardType.ZipZap;
            public int Size => 3;
            public int ManaCost => 3;
            public CardTarget Target => CardTarget.OwnBoard;
            public int SearchRadius => 4;
        }

        public class ZipZap_Max : IZipZap
        {
            public CardType Type => CardType.ZipZap_Max;
            public int Size => 4;
            public int ManaCost => 5;
            public CardTarget Target => CardTarget.OwnBoard;
            public int SearchRadius => 4;
        }

        public class OpponentFlagErase : ICardConfig
        {
            public CardType Type => CardType.OpponentFlagErase;
            public int Size => 3;
            public int ManaCost => 3;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        public class OpponentFlagErase_Max : ICardConfig
        {
            public CardType Type => CardType.OpponentFlagErase_Max;
            public int Size => 4;
            public int ManaCost => 5;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        public class OpponentFlagReshuffle : ICardConfig
        {
            public CardType Type => CardType.OpponentFlagReshuffle;
            public int Size => 3;
            public int ManaCost => 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        public class OpponentFlagReshuffle_Max : ICardConfig
        {
            public CardType Type => CardType.OpponentFlagReshuffle_Max;
            public int Size => 4;
            public int ManaCost => 4;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        public class OpponentBomb : ICardConfig
        {
            public CardType Type => CardType.OpponentBomb;
            public int Size => 0;
            public int ManaCost => 2;
            public CardTarget Target => CardTarget.OpponentBoard;
        }

        public interface ISmoke : ICardConfig
        {
            int Duration { get; }
        }

        public class Smoke : ISmoke
        {
            public CardType Type => CardType.Smoke;
            public int Size => 3;
            public int ManaCost => 3;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int Duration => 3;
        }

        public class Smoke_Max : ISmoke
        {
            public CardType Type => CardType.Smoke_Max;
            public int Size => 4;
            public int ManaCost => 5;
            public CardTarget Target => CardTarget.OpponentBoard;
            public int Duration => 3;
        }

        static CardsConfigs()
        {
            var entries = new Dictionary<CardType, ICardConfig>();
            _entries = entries;

            AddEntry(new Trebuchet());
            AddEntry(new Trebuchet_Max());
            AddEntry(new Bloodhound());
            AddEntry(new Bloodhound_Max());
            AddEntry(new TrebuchetAimer());
            AddEntry(new TrebuchetAimer_Max());
            AddEntry(new ErosionDozer());
            AddEntry(new ErosionDozer_Max());
            AddEntry(new Gravedigger());
            AddEntry(new ZipZap());
            AddEntry(new ZipZap_Max());
            AddEntry(new OpponentBomb());
            AddEntry(new OpponentFlagErase());
            AddEntry(new OpponentFlagErase_Max());
            AddEntry(new OpponentFlagReshuffle());
            AddEntry(new OpponentFlagReshuffle_Max());
            AddEntry(new Smoke());
            AddEntry(new Smoke_Max());

            return;

            void AddEntry(ICardConfig config)
            {
                entries[config.Type] = config;
            }
        }

        private static readonly IReadOnlyDictionary<CardType, ICardConfig> _entries;

        public static ICardConfig ToConfig(this CardType type)
        {
            return _entries[type];
        }

        public static T ToConfig<T>(this CardType type) where T : ICardConfig
        {
            return (T)_entries[type];
        }
    }
}