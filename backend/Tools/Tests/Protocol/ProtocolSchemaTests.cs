using FluentAssertions;
using Xunit;

namespace Tests.Protocol;

/// <summary>
/// Union tags are explicit: [MemoryPackUnion(tag, typeof(T))] next to the types in shared, one declaration read by
/// both the client and the backend. A [MemoryPackable] implementation of a union base without a tag, or a tag or
/// type declared twice, fails here (a duplicated tag also fails the MemoryPack generator at compile time).
/// The bytes of every tag are pinned by ProtocolBytesTests.
/// </summary>
public class ProtocolSchemaTests
{
    [Fact]
    public void UnionTags_AreUniquePerBase()
    {
        foreach (var unionBase in ProtocolSchema.UnionBases())
        {
            var tags = ProtocolSchema.UnionTags(unionBase);
            tags.Select(t => t.Tag).Should().OnlyHaveUniqueItems(unionBase.FullName);
            tags.Select(t => t.Type).Should().OnlyHaveUniqueItems(unionBase.FullName);
        }
    }

    [Fact]
    public void EveryImplementation_HasUnionTag()
    {
        var concrete = ProtocolSchema.PackableTypes()
                                     .Where(t => t.IsInterface == false && t.IsAbstract == false)
                                     .ToList();

        foreach (var unionBase in ProtocolSchema.UnionBases())
        {
            var tagged = ProtocolSchema.UnionTags(unionBase).Select(t => t.Type).ToHashSet();
            var untagged = concrete.Where(t => unionBase.IsAssignableFrom(t) && tagged.Contains(t) == false)
                                   .Select(t => t.FullName);

            untagged.Should().BeEmpty($"every [MemoryPackable] {unionBase.Name} needs a [MemoryPackUnion] tag");
        }
    }
}
