using MemoryPack;
using Shared;

namespace Framework.Tests.Protocol;

[MemoryPackUnionFormatter(typeof(IStatePayload))]
[MemoryPackUnion(StatePayloadTags.Empty, typeof(EmptyStatePayload))]
public partial class TestStatePayloadUnion
{
}
