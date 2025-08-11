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
        
    }

    public partial class CardUsePayload
    {
        [MemoryPackable]
        public partial class ZipZap : ICardUsePayload
        {
        }
        
        [MemoryPackable]
        public partial class Bloodhound : ICardUsePayload
        {
        }
        
        [MemoryPackable]
        public partial class ErosionDozer : ICardUsePayload
        {
        }
        
        [MemoryPackable]
        public partial class Gravedigger : ICardUsePayload
        {
        }
        
        [MemoryPackable]
        public partial class Trebuchet : ICardUsePayload
        {
        }
        
        [MemoryPackable]
        public partial class TrebuchetAimer : ICardUsePayload
        {
        }
    }
}