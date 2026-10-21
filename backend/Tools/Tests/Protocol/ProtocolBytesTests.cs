using FluentAssertions;
using System.Runtime.CompilerServices;
using MemoryPack;
using Shared;
using Xunit;

namespace Tests.Protocol;

/// <summary>
/// The bytes the backend writes for every [MemoryPackable] type of shared (as itself) and for every union
/// member (through its base, with the tag) are pinned in protocol_bytes.golden.txt. The client reads the same file
/// in the EditMode test Internal.Tests.ProtocolBytesTests: it must read every line back to the same type and write
/// it back - the client (Unity types, its MemoryPack build) and the backend agree on the wire.
/// Values come from SharedSerializationTests.ValueFiller (deterministic, non-default members).
/// Padding: MemoryPack copies unmanaged structs as raw memory, padding included (KeyValuePair&lt;int, long&gt; of a
/// Dictionary&lt;enum, long&gt; has 4 bytes after the key, CardType? has 3 after HasValue). Those bytes are whatever
/// the writer had on the stack: zeros on the backend so far, garbage in Unity. So a line passes when the bytes are
/// equal, or when they have the same length and decode to the same value (ProtocolValueText). A changed tag, member
/// order, member type or length still fails.
/// Run with UPDATE_PROTOCOL_GOLDEN=1 to rewrite the golden file after an intended protocol change.
/// </summary>
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
        var failures = Compare(expected, actual);

        if (failures.Count == 0)
            return;

        Assert.Fail($"Protocol bytes differ from {path} (set {UpdateVariable}=1 to rewrite it):\n" +
                    string.Join("\n", failures));
    }

    // Padding bytes are not part of the format: a line whose bytes differ only there decodes to the same value.
    [Fact]
    public void PaddingOnlyDifference_IsAccepted_OtherDifference_IsNot()
    {
        var stats = new MatchPlayerStats();
        stats.Counters[UserStatType.FlagsSet] = 4;
        var bytes = MemoryPackSerializer.Serialize(stats);

        // Member count, then Counters: count + KeyValuePair<int, long> (key, 4 padding bytes, value).
        const int keyOffset = 1 + 4;
        BitConverter.ToInt32(bytes, keyOffset).Should().Be((int)UserStatType.FlagsSet);
        BitConverter.ToInt64(bytes, keyOffset + 8).Should().Be(4);
        var padding = (byte[])bytes.Clone();
        padding[keyOffset + 4] = 0xCD;
        padding[keyOffset + 7] = 0xCD;
        var value = (byte[])bytes.Clone();
        value[keyOffset + 8] = 0x05;

        var line = $"type {typeof(MatchPlayerStats).FullName} ";

        Compare(line + Hex(bytes) + "\n", line + Hex(padding) + "\n").Should().BeEmpty();
        Compare(line + Hex(bytes) + "\n", line + Hex(value) + "\n").Should().NotBeEmpty();
    }

    // Lines are "<kind> <names...> <hex>"; the key is everything before the hex.
    public static List<string> Compare(string expected, string actual)
    {
        var expectedLines = Parse(expected);
        var actualLines = Parse(actual);
        var failures = new List<string>();

        foreach (var key in expectedLines.Keys.Except(actualLines.Keys))
            failures.Add($"- {key} {expectedLines[key]}");

        foreach (var key in actualLines.Keys.Except(expectedLines.Keys))
            failures.Add($"+ {key} {actualLines[key]}");

        foreach (var key in expectedLines.Keys.Intersect(actualLines.Keys))
        {
            var expectedHex = expectedLines[key];
            var actualHex = actualLines[key];

            if (expectedHex == actualHex)
                continue;

            if (expectedHex.Length == actualHex.Length && SameValue(key, expectedHex, actualHex))
                continue;

            failures.Add($"- {key} {expectedHex}\n+ {key} {actualHex}");
        }

        return failures;
    }

    private static Dictionary<string, string> Parse(string text)
    {
        return text.Split('\n')
                   .Where(l => l.Length > 0)
                   .ToDictionary(l => l[..l.LastIndexOf(' ')], l => l[(l.LastIndexOf(' ') + 1)..]);
    }

    private static bool SameValue(string key, string expectedHex, string actualHex)
    {
        var parts = key.Split(' ');
        var type = ProtocolSchema.GetType(parts[1]);

        try
        {
            var expected = MemoryPackSerializer.Deserialize(type, Convert.FromHexString(expectedHex));
            var actual = MemoryPackSerializer.Deserialize(type, Convert.FromHexString(actualHex));
            return ProtocolValueText.Of(expected) == ProtocolValueText.Of(actual);
        }
        catch (MemoryPackSerializationException)
        {
            return false;
        }
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
