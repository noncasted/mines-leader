using System.Runtime.CompilerServices;
using FluentAssertions;
using Shared;
using Xunit;

namespace Tests.Protocol;

/// <summary>
/// Union tags are explicit: [MemoryPackUnion(tag, typeof(T))] next to the types in shared, one declaration read by
/// both the client and the backend. A [MemoryPackable] implementation of a union base without a tag, or a tag or
/// type declared twice, fails here (a duplicated tag also fails the MemoryPack generator at compile time).
/// The bytes of every tag are pinned by ProtocolBytesTests.
/// The wire schema (union tags, members of [MemoryPackable] types, enum values) is pinned in
/// protocol_schema.golden.txt: a changed tag or member shows up as a diff of the golden file in review.
/// Run with UPDATE_PROTOCOL_GOLDEN=1 to rewrite the golden file after an intended protocol change.
/// ProtocolHash.Value, sent by the client in the socket upgrade, is the hash of that schema.
/// </summary>
public class ProtocolSchemaTests
{
    private const string UpdateVariable = "UPDATE_PROTOCOL_GOLDEN";

    [Fact]
    public void Schema_MatchesGolden()
    {
        var actual = ProtocolSchema.BuildText();
        var path = GoldenPath();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
            File.WriteAllText(path, actual);

        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");

        if (expected == actual)
            return;

        var expectedLines = expected.Split('\n').ToHashSet();
        var actualLines = actual.Split('\n').ToHashSet();
        var removed = expectedLines.Except(actualLines).Select(l => "- " + l);
        var added = actualLines.Except(expectedLines).Select(l => "+ " + l);

        Assert.Fail($"Protocol schema differs from {path} (set {UpdateVariable}=1 to rewrite it):\n" +
                    string.Join("\n", removed.Concat(added)));
    }

    [Fact]
    public void ProtocolHash_MatchesSchema()
    {
        var hash = ProtocolSchema.ComputeHash(ProtocolSchema.BuildText());

        ProtocolHash.Value.Should().Be(hash,
            "the protocol schema changed: set ProtocolHash.Value = \"{0}\" in shared/Protocol/ProtocolHash.cs",
            hash);
    }

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

    private static string GoldenPath([CallerFilePath] string sourcePath = "")
    {
        return Path.Combine(Path.GetDirectoryName(sourcePath)!, "protocol_schema.golden.txt");
    }
}
