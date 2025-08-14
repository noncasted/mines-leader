using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardUsePayload.ZipZap))]
    [MemoryPackUnion(1, typeof(CardUsePayload.Bloodhound))]
    [MemoryPackUnion(2, typeof(CardUsePayload.ErosionDozer))]
    [MemoryPackUnion(3, typeof(CardUsePayload.Gravedigger))]
    [MemoryPackUnion(4, typeof(CardUsePayload.Trebuchet))]
    [MemoryPackUnion(5, typeof(CardUsePayload.TrebuchetAimer))]
    public partial interface ICardUsePayload
    {
        CardType Type { get; set; }
    }
    
    public interface IBoardCardUsePayload : ICardUsePayload
    {
        Position Position { get; set; }
    }

    public partial class CardUsePayload
    {
        [MemoryPackable]
        public partial class ZipZap : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Bloodhound : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class ErosionDozer : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Gravedigger : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class Trebuchet : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }

        [MemoryPackable]
        public partial class TrebuchetAimer : IBoardCardUsePayload
        {
            public CardType Type { get; set; }
            public Position Position { get; set; }
        }
    }
}