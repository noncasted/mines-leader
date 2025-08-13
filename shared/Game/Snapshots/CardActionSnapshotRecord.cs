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
    }

    public partial class CardActionSnapshot
    {
        [MemoryPackable]
        public partial class ZipZap : ICardActionData
        {
        }
        
        [MemoryPackable]
        public partial class Bloodhound : ICardActionData
        {
        }
        
        [MemoryPackable]
        public partial class ErosionDozer : ICardActionData
        {
        }
        
        [MemoryPackable]
        public partial class Gravedigger : ICardActionData
        {
        }
        
        [MemoryPackable]
        public partial class Trebuchet : ICardActionData
        {
        }
        
        [MemoryPackable]
        public partial class TrebuchetAimer : ICardActionData
        {
        }
    }
}