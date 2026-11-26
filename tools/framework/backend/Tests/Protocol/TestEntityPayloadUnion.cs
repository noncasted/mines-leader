using MemoryPack;
using Shared;

namespace Framework.Tests.Protocol;

// The payload unions a project declares next to its payload types: here only the framework members.
[MemoryPackUnionFormatter(typeof(IEntityPayload))]
[MemoryPackUnion(0, typeof(TestEntityPayload))]
public partial class TestEntityPayloadUnion
{
}
