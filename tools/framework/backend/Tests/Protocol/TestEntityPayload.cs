using MemoryPack;
using Shared;

namespace Framework.Tests.Protocol;

[MemoryPackable]
public partial class TestEntityPayload : IEntityPayload
{
    public int Kind { get; set; }
}
