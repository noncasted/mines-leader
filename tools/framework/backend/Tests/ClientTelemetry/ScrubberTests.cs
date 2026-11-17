using System.Text.Json;
using FluentAssertions;
using Game.ClientTelemetry;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

public class ScrubberTests
{
    public static TheoryData<string> CaseNames()
    {
        return new TheoryData<string>(ReadCorpus().Keys);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void CorpusCase_LosesPersonalData_AndKeepsTheTemplate(string name)
    {
        var corpusCase = ReadCorpus()[name];

        var scrubbed = Scrubber.Scrub(corpusCase.Text);

        foreach (var forbidden in corpusCase.Forbidden)
            scrubbed.Should().NotContain(forbidden);

        foreach (var kept in corpusCase.Kept)
            scrubbed.Should().Contain(kept);
    }

    // The client scrubs first, the server scrubs what came from the client.
    [Theory]
    [MemberData(nameof(CaseNames))]
    public void SecondPass_ChangesNothing(string name)
    {
        var once = Scrubber.Scrub(ReadCorpus()[name].Text);

        Scrubber.Scrub(once).Should().Be(once);
    }

    [Fact]
    public void Corpus_HasCases()
    {
        ReadCorpus().Should().HaveCountGreaterThan(40);
    }

    [Theory]
    [InlineData("NullReferenceException: Object reference not set to an instance of an object")]
    [InlineData("Game.Swarm.Player:Update ()\nUnityEngine.Debug:LogError (object)")]
    [InlineData("UnityEngine.Vector3:Normalize ()")]
    [InlineData("System.Collections.Generic.List`1:Add (T)")]
    [InlineData("Game.Foo:Bar (System.Int32,object[])")]
    public void CleanText_StaysAsItIs(string text)
    {
        Scrubber.Scrub(text).Should().Be(text);
    }

    [Fact]
    public void DifferentIdsOfOneTemplate_BecomeOneText()
    {
        Scrubber.Scrub("Player 123 lost room 4567").Should().Be(Scrubber.Scrub("Player 98 lost room 1"));
    }

    [Fact]
    public void WorstCaseText_IsScrubbedFast()
    {
        var inputs = new[]
        {
            new string('a', ClientTelemetryLimits.MaxStackLength),
            new string('@', ClientTelemetryLimits.MaxStackLength),
            new string('/', ClientTelemetryLimits.MaxStackLength),
            new string('%', ClientTelemetryLimits.MaxStackLength),
            new string(':', ClientTelemetryLimits.MaxStackLength),
            string.Concat(Enumerable.Repeat("a.", ClientTelemetryLimits.MaxStackLength / 2)),
            string.Concat(Enumerable.Repeat("a=", ClientTelemetryLimits.MaxStackLength / 2)),
            string.Concat(Enumerable.Repeat("'a", ClientTelemetryLimits.MaxStackLength / 2))
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var input in inputs)
            Scrubber.Scrub(input);

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    private static Dictionary<string, CorpusCase> ReadCorpus()
    {
        using var document = JsonDocument.Parse(ClientTelemetryContractFiles.ReadBytes("pii_corpus.json"));

        return document.RootElement.GetProperty("cases").EnumerateArray().ToDictionary(
            item => item.GetProperty("name").GetString()!,
            item => new CorpusCase(
                item.GetProperty("text").GetString()!,
                Strings(item.GetProperty("forbidden")),
                Strings(item.GetProperty("kept"))));
    }

    private static string[] Strings(JsonElement array)
    {
        return array.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private sealed record CorpusCase(string Text, string[] Forbidden, string[] Kept);
}
