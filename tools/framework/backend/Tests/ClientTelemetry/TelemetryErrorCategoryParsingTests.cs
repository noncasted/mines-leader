using FluentAssertions;
using Game.ClientTelemetry;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The category is the name of a type of the code: a closed alphabet like the names of the metrics, so it cannot carry a free text.
// One that breaks it is dropped alone, the error stays without a category.
public class TelemetryErrorCategoryParsingTests
{
    [Theory]
    [InlineData("Global.Network.PeerSocket")]
    [InlineData("Internal.LogsTests.Owner")]
    [InlineData("Global.Network.Pool<Packet>")]
    [InlineData("Global.Cache<Item, Int32>")]
    [InlineData("_Game.Run2")]
    public void ANameOfAType_IsKept(string category)
    {
        Parse(category).Should().Be(category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Global.Network ivan")]
    [InlineData("ivan@example.com")]
    [InlineData("token=abc")]
    [InlineData("Global.Network\nPeer")]
    [InlineData("Игрок")]
    [InlineData("1Global")]
    [InlineData("https://game.example/x")]
    public void AnythingElse_IsDropped_TheErrorStays(string category)
    {
        Parse(category).Should().BeNull();
    }

    [Fact]
    public void TheLength_IsLimited()
    {
        Parse(new string('A', ClientTelemetryLimits.MaxCategoryLength)).Should().HaveLength(ClientTelemetryLimits.MaxCategoryLength);
        Parse(new string('A', ClientTelemetryLimits.MaxCategoryLength + 1)).Should().BeNull();
    }

    [Fact]
    public void None_IsNone()
    {
        Parse(null).Should().BeNull();
    }

    private static string? Parse(string? category)
    {
        var batch = TelemetryWire.Batch(Guid.NewGuid(), errors: [TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Error, "Join <n> failed", category: category)]);
        var parsed = TelemetryBatchParser.Parse(batch);

        parsed.Rejection.Should().BeNull();
        return parsed.Errors.Single().Category;
    }
}
