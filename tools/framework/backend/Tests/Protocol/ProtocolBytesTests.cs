using System.Runtime.CompilerServices;
using MemoryPack;
using Xunit;

namespace Framework.Tests.Protocol;

// The bytes the backend writes for every [MemoryPackable] type of shared (as itself) and for every union
// member (through its base, with the tag) are pinned in protocol_bytes.golden.txt: a changed tag, member order or
// member type shows up as a diff of the golden file in review (the projects pin their whole protocol the same way,
// and their client reads the file back in an EditMode test).
// Values come from SharedSerializationTests.ValueFiller (deterministic, non-default members).
// Run with UPDATE_PROTOCOL_GOLDEN=1 to rewrite the golden file after an intended protocol change.
public class ProtocolBytesTests
{
    private const string UpdateVariable = "UPDATE_PROTOCOL_GOLDEN";

    [Fact]
    public void Bytes_MatchGolden()
    {
        var actual = BuildText();
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

        Assert.Fail($"Protocol bytes differ from {path} (set {UpdateVariable}=1 to rewrite it):\n" +
                    string.Join("\n", removed.Concat(added)));
    }

    // "type <type> <hex>" - the value serialized as its own type;
    // "union <base> <tag> <type> <hex>" - the value serialized through the union base.
    public static string BuildText()
    {
        var lines = new List<string>();

        foreach (var type in ProtocolSchema.PackableTypes().Where(IsConcrete))
        {
            var value = new SharedSerializationTests.ValueFiller().Create(type, 0);
            lines.Add($"type {type.FullName} {Hex(MemoryPackSerializer.Serialize(type, value))}");
        }

        foreach (var unionBase in ProtocolSchema.UnionBases())
        {
            foreach (var (tag, type) in ProtocolSchema.UnionTags(unionBase))
            {
                var value = new SharedSerializationTests.ValueFiller().Create(type, 0);
                var bytes = MemoryPackSerializer.Serialize(unionBase, value);
                lines.Add($"union {unionBase.FullName} {tag} {type.FullName} {Hex(bytes)}");
            }
        }

        lines.Sort(StringComparer.Ordinal);
        return string.Join("\n", lines) + "\n";
    }

    private static bool IsConcrete(Type type)
    {
        return type.IsInterface == false && type.IsAbstract == false && type.IsGenericTypeDefinition == false;
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GoldenPath([CallerFilePath] string sourcePath = "")
    {
        return Path.Combine(Path.GetDirectoryName(sourcePath)!, "protocol_bytes.golden.txt");
    }
}
