using MemoryPack;
using Shared;

namespace Framework.Tests.Protocol;

[MemoryPackable]
public partial class TestEventPayload : IEventPayload
{
    public int Value { get; set; }
}
