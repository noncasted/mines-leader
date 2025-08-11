using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(CardActionSnapshot.ZipZap))]
    [MemoryPackUnion(1, typeof(CardActionSnapshot.Bloodhound))]
    [MemoryPackUnion(2, typeof(CardActionSnapshot.ErosionDozer))]
    [MemoryPackUnion(3, typeof(CardActionSnapshot.Gravedigger))]
    [MemoryPackUnion(4, typeof(CardActionSnapshot.Trebuchet))]
    [MemoryPackUnion(5, typeof(CardActionSnapshot.TrebuchetAimer))]
    public partial interface ICardActionData
    {
        CardType Type { get; set; }
    }

    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class ZipZap : ICardActionData
        {
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class Bloodhound : ICardActionData
        {
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class ErosionDozer : ICardActionData
        {
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class Gravedigger : ICardActionData
        {
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class Trebuchet : ICardActionData
        {
            public CardType Type { get; set; }
        }
        
        [MemoryPackable]
        public partial class TrebuchetAimer : ICardActionData
        {
            public CardType Type { get; set; }
        }
    }
}