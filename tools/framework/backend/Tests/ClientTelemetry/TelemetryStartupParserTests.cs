using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The client is not trusted: what reaches the storage is a tree with valid numbers and scrubbed names.
public class TelemetryStartupParserTests
{
    private static SharedClientTelemetry.StartupSpan Span(int parent, string? name = "Scope: Registry", double start = 0, double duration = 10)
    {
        return new SharedClientTelemetry.StartupSpan { ParentIndex = parent, Name = name!, StartMs = start, DurationMs = duration, Frames = 1 };
    }

    private static TelemetryStartupTrace Parse(params SharedClientTelemetry.StartupSpan[] spans)
    {
        return TelemetryStartupParser.Parse(new SharedClientTelemetry.StartupTrace { Name = "Boot", DurationMs = 100, Frames = 5, Spans = spans });
    }

    [Fact]
    public void ANameOfAScope_StaysAsItIs()
    {
        Parse(Span(-1, "Scope: Registry"), Span(0, "Registry scene"), Span(1, "Sprites: Game_UISprites")).Spans
            .Select(span => span.Name).Should().Equal("Scope: Registry", "Registry scene", "Sprites: Game_UISprites");
    }

    [Theory]
    [InlineData("GET https://api.example.com/users/12345?token=abc123", "GET <url>")]
    [InlineData("POST https://10.0.0.1:8080/profile/ivan", "POST <url>")]
    [InlineData("GET /api/users/12345/inventory", "GET <path>")]
    public void AUrlInAName_IsScrubbedOut(string name, string expected)
    {
        Parse(Span(-1, name)).Spans[0].Name.Should().Be(expected);
    }

    [Fact]
    public void AnEmailAndAGuidInAName_AreScrubbedOut()
    {
        var names = Parse(Span(-1, "Load user ivan@example.com"), Span(-1, "Load 3f2504e0-4f89-41d3-9a0c-0305e82c3301")).Spans.Select(span => span.Name).ToArray();

        names[0].Should().Be("Load user <email>");
        names[1].Should().Be("Load <guid>");
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, double.NaN)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(1, -1)]
    [InlineData(1, 1e300)]
    public void ABadNumber_DropsTheSpanWithItsChildren_AndIsCounted(double start, double duration)
    {
        var trace = Parse(Span(-1), Span(0, "bad", start, duration), Span(1, "child of bad"), Span(0, "good"));

        trace.Spans.Select(span => span.Name).Should().Equal("Scope: Registry", "good");
        trace.Spans[1].ParentIndex.Should().Be(0);
        trace.RejectedSpans.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(-2)]
    public void AParentNotBelowItsIndex_IsRejected(int parent)
    {
        var trace = Parse(Span(-1), Span(parent));

        trace.Spans.Should().HaveCount(parent == 0 ? 2 : 1);
    }

    [Fact]
    public void NegativeFramesAndAllocations_AreRejected()
    {
        var trace = Parse(
            new SharedClientTelemetry.StartupSpan { ParentIndex = -1, Name = "a", Frames = -1 },
            new SharedClientTelemetry.StartupSpan { ParentIndex = -1, Name = "b", AllocBytes = -1 });

        trace.Spans.Should().BeEmpty();
        trace.RejectedSpans.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a\nb")]
    [InlineData("a\u0007b")]
    public void AnEmptyNameOrAControlSymbol_RejectsTheSpan(string? name)
    {
        var trace = Parse(Span(-1, name));

        trace.Spans.Should().BeEmpty();
        trace.RejectedSpans.Should().Be(1);
    }

    [Fact]
    public void ANullSpanAndNullLists_DoNotThrow()
    {
        Parse(null!, Span(-1)).RejectedSpans.Should().Be(1);

        var empty = TelemetryStartupParser.Parse(new SharedClientTelemetry.StartupTrace());

        empty.Spans.Should().BeEmpty();
        empty.Name.Should().BeEmpty();
    }

    [Fact]
    public void ALongName_IsCut_AndCounted()
    {
        var trace = Parse(Span(-1, new string('a', ClientTelemetryLimits.MaxStartupNameLength)), Span(-1, "Stage " + new string('b', 20) + " " + new string('c', 400)));

        trace.Spans[0].Name.Length.Should().BeLessThanOrEqualTo(ClientTelemetryLimits.MaxStartupNameLength);
        trace.Spans[1].Name.Length.Should().BeLessThanOrEqualTo(ClientTelemetryLimits.MaxStartupNameLength);
        trace.Spans[1].Name.Should().StartWith("Stage ");
        trace.CutNames.Should().Be(1);
    }

    [Fact]
    public void OverTheSpans_TheTailIsCut_AndCounted()
    {
        var spans = Enumerable.Range(0, ClientTelemetryLimits.MaxStartupSpans + 7).Select(index => Span(index - 1, "a")).ToArray();

        var trace = Parse(spans);

        trace.Spans.Should().HaveCount(ClientTelemetryLimits.MaxStartupSpans);
        trace.DroppedSpans.Should().Be(7);
    }

    [Fact]
    public void OverTheBytes_TheTailIsCut_AndCounted()
    {
        var name = string.Join(' ', Enumerable.Repeat("abcdefgh", 13));
        var spans = Enumerable.Range(0, ClientTelemetryLimits.MaxStartupSpans).Select(index => Span(-1, $"{name} x")).ToArray();

        var trace = Parse(spans);

        trace.Spans.Count.Should().BeLessThan(spans.Length);
        trace.DroppedSpans.Should().Be(spans.Length - trace.Spans.Count);
        (trace.Spans.Count * (long)ClientTelemetryLimits.MaxStartupNameLength).Should().BeLessThanOrEqualTo(ClientTelemetryLimits.MaxStartupBytes);
    }

    [Fact]
    public void ATypicalStart_FitsTheLimits()
    {
        var spans = Enumerable.Range(0, 365).Select(index => Span(index == 0 ? -1 : 0, "Instantiate: A_Name_Of_The_Typical_Length")).ToArray();

        var trace = Parse(spans);

        trace.Spans.Should().HaveCount(365);
        trace.DroppedSpans.Should().Be(0);
    }

    [Fact]
    public void ABadNumberOfTheTrace_IsZero_AndCounted()
    {
        var trace = TelemetryStartupParser.Parse(new SharedClientTelemetry.StartupTrace { Name = "Boot", DurationMs = double.NaN, Frames = -3 });

        trace.DurationMs.Should().Be(0);
        trace.Frames.Should().Be(0);
        trace.RejectedSpans.Should().Be(2);
    }

    [Fact]
    public void TheNameOfTheTrace_IsCheckedLikeTheNameOfASpan()
    {
        TelemetryStartupParser.Parse(new SharedClientTelemetry.StartupTrace { Name = "GET https://a.example.com/x?y=1" }).Name.Should().Be("GET <url>");
        TelemetryStartupParser.Parse(new SharedClientTelemetry.StartupTrace { Name = "a\nb" }).Name.Should().BeEmpty();
    }

    [Fact]
    public void AFileOfThePage_KeepsItsNameSizeAndCache()
    {
        var file = Span(0, "File");
        file.File = "Build.wasm.unityweb";
        file.TransferBytes = 12_000_000;
        file.Cached = true;

        var span = Parse(Span(-1, "Page"), file).Spans[1];

        span.File.Should().Be("Build.wasm.unityweb");
        span.TransferBytes.Should().Be(12_000_000);
        span.Cached.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://cdn.example.com/Build.wasm")]
    [InlineData("Build/Build.wasm")]
    [InlineData("Build.wasm?v=1")]
    [InlineData("ivan@mail.ru")]
    [InlineData("")]
    public void AFileOutOfTheFormOfAName_IsDropped_TheSpanStays(string name)
    {
        var file = Span(-1, "File");
        file.File = name;

        var trace = Parse(file);

        trace.Spans.Should().ContainSingle().Which.File.Should().BeNull();
        trace.RejectedSpans.Should().Be(0);
    }

    [Fact]
    public void AFileNameOverTheLimit_IsDropped()
    {
        var file = Span(-1, "File");
        file.File = new string('a', ClientTelemetryLimits.MaxStartupFileLength + 1) + ".js";

        Parse(file).Spans[0].File.Should().BeNull();
    }

    [Fact]
    public void NegativeTransferBytes_DropTheSpan()
    {
        var file = Span(-1, "File");
        file.TransferBytes = -1;

        Parse(file).RejectedSpans.Should().Be(1);
    }
}
