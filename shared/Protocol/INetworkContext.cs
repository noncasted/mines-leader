using MemoryPack;

namespace Shared
{
    // Every message of the protocol. Union tags are declared next to the types, in each group file, as
    // [MemoryPackUnion(tag, typeof(T))] on a partial INetworkContext. The tags are the wire format: never renumber or
    // reuse one; a removed type leaves a hole. A type without a tag fails Tests.Protocol.ProtocolSchemaTests, a
    // duplicated tag fails the MemoryPack generator; protocol_bytes.golden.txt pins the bytes.
    [MemoryPackable]
    public partial interface INetworkContext
    {
    }
}