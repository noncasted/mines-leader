using MemoryPack;
using Shared;

namespace Framework.Tests.Protocol;

[MemoryPackUnionFormatter(typeof(IEventPayload))]
[MemoryPackUnion(0, typeof(TestEventPayload))]
public partial class TestEventPayloadUnion
{
}
