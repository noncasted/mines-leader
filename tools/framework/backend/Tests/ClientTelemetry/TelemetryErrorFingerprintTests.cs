using FluentAssertions;
using Game.ClientTelemetry;
using Infrastructure.State;
using Newtonsoft.Json;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The rows come through the parser and the buffer: what is hashed is the text the storage gets, already scrubbed.
public class TelemetryErrorFingerprintTests
{
    private const string _message = "NullReferenceException: Object reference not set";

    private static readonly DateTime _now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Theory]
    [InlineData("at Game.Foo.Bar () [0x00012] in /builds/app/Assets/Foo.cs:123", "at Game.Foo.Bar () [0x0009f] in /other/place/Assets/Foo.cs:456")]
    [InlineData("Game.Foo:Bar (int) (at Assets/Scripts/Foo.cs:42)", "Game.Foo:Bar (int) (at Assets/Scripts/Foo.cs:43)")]
    [InlineData("Game.Foo:Bar ()\nat wasm-function[1234]:0x5678", "Game.Foo:Bar ()\nat wasm-function[9876]:0x4321")]
    [InlineData("at Object.foo (https://game.example/Build/app.framework.js:1:2345)", "at Object.foo (https://cdn.example/Build/other.framework.js:1:9999)")]
    [InlineData("at foo (app.js:12:34)", "at foo (app.js:56:78)")]
    [InlineData("at Game.Foo.Bar() in Foo.cs:line 12", "at Game.Foo.Bar() in Foo.cs:line 345")]
    [InlineData("at Game.Foo.Bar () <0x00012 + 0x0001f> in <abc123def456abc123def456abc123de>:0", "at Game.Foo.Bar () <0x00099 + 0x000ff> in <fed321fed321fed321fed321fed321fe>:0")]
    [InlineData("Game.Foo:Bar ()\r\n  Game.Foo:Baz ()", "Game.Foo:Bar ()\nGame.Foo:Baz ()")]
    [InlineData("at Game.Foo.Bar + 12", "at Game.Foo.Bar + 0x0c")]
    [InlineData("Game.Foo:Bar ()\nat wasm-function[1234]:0x5678", "Game.Foo:Bar ()\nat wasm-function[1234]:5678")]
    public void TheSame_AtOtherLinesAndAddresses(string stack, string other)
    {
        Row(_message, stack).Fingerprint.Should().Be(Row(_message, other).Fingerprint);
    }

    [Theory]
    [InlineData("Game.Foo:Bar ()", "Game.Foo:Baz ()")]
    [InlineData("Game.Foo:Bar (int) (at Assets/Foo.cs:42)", "Game.Other:Bar (int) (at Assets/Foo.cs:42)")]
    [InlineData("at Game.Foo.Bar () [0x00012] in /app/Foo.cs:1", "at Game.Foo.Baz () [0x00012] in /app/Foo.cs:1")]
    [InlineData("Game.Foo:Bar ()\nat wasm-function[1]:0x5", "Game.Foo:Baz ()\nat wasm-function[1]:0x5")]
    [InlineData("Game.Foo:Bar ()", "")]
    public void AnotherMethod_IsAnotherFingerprint(string stack, string other)
    {
        Row(_message, stack).Fingerprint.Should().NotBe(Row(_message, other).Fingerprint);
    }

    [Fact]
    public void AnEmptyStack_StillGetsAFingerprint_ByTheTemplateOfTheMessage()
    {
        var fingerprint = Row("IndexOutOfRangeException: Index 5 out of range", "").Fingerprint;

        fingerprint.Should().NotBeNullOrEmpty();
        Row("IndexOutOfRangeException: Index 77 out of range", "").Fingerprint.Should().Be(fingerprint);
        Row("KeyNotFoundException: Index 5 out of range", "").Fingerprint.Should().NotBe(fingerprint);
    }

    [Fact]
    public void AMissingStack_IsAnEmptyStack()
    {
        Rows(TelemetryWire.Batch(Guid.NewGuid(), errors: [TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Exception, _message, null)])).Single().Fingerprint.Should().Be(Row(_message, "").Fingerprint);
    }

    [Fact]
    public void TheLevel_IsAPartOfTheFingerprint()
    {
        Row(_message, "", "error").Fingerprint.Should().NotBe(Row(_message, "", "exception").Fingerprint);
    }

    // The personal data is gone before the fingerprint: another player gives the same group.
    [Fact]
    public void TheMessage_IsCountedAfterTheScrubber()
    {
        var row = Row("HttpException: no answer for ivan@example.com at 10.1.2.3, session 3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01", "");
        var other = Row("HttpException: no answer for maria@example.org at 192.168.0.7, session 11111111-2222-4333-8444-555555555555", "");

        row.Message.Should().NotContain("ivan").And.NotContain("10.1.2.3").And.NotContain("3f2b8c1e");
        row.Fingerprint.Should().Be(other.Fingerprint);
    }

    // Pinned: the groups live in stored rows and are compared between hosts and restarts. string.GetHashCode would change each run.
    [Fact]
    public void IsTheSameInEveryProcess()
    {
        TelemetryErrorFingerprint
            .Of(TelemetryErrorLevel.Exception, "NullReferenceException: Boom <n>", "at A:B () [<id>]", null)
            .Should().Be("7bcef580d8fb67fb", "the first 16 hex digits of the SHA-256 of the normalized text, the same without a category as before it");
    }

    // The same template of the logger in two categories is two kinds of error.
    [Fact]
    public void TheCategory_IsAPartOfTheFingerprint()
    {
        var first = Row("Join <n> failed", "", category: "Global.Lobbies.LobbyJoin").Fingerprint;
        var second = Row("Join <n> failed", "", category: "Global.Network.PeerSocket").Fingerprint;
        var none = Row("Join <n> failed", "").Fingerprint;

        first.Should().NotBe(second);
        first.Should().NotBe(none);
        Row("Join <n> failed", "", category: "Global.Lobbies.LobbyJoin").Fingerprint.Should().Be(first);
    }

    [Fact]
    public void TheCategory_GoesThroughTheJsonOfTheStates()
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());
        buffer.Apply(TelemetryBatchParser.Parse(Body(_message, "", "error", "Global.Network.PeerSocket")), _now);

        var settings = JsonStateSettings.CreateBase();
        var json = JsonConvert.SerializeObject(TelemetrySessionLogState.Of(buffer), settings);
        var log = JsonConvert.DeserializeObject<TelemetrySessionLogState>(json, settings)!;

        log.Errors.Single().Category.Should().Be("Global.Network.PeerSocket");
    }

    // A row stored before the category: read with none.
    [Fact]
    public void ARowWithoutTheCategory_ReadsAsNone()
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());
        buffer.Apply(TelemetryBatchParser.Parse(Body(_message, "", "error")), _now);

        var settings = JsonStateSettings.CreateBase();
        var json = JsonConvert.SerializeObject(TelemetrySessionLogState.Of(buffer), settings);
        var old = System.Text.RegularExpressions.Regex.Replace(json, @",?""Category"":null", "");
        var log = JsonConvert.DeserializeObject<TelemetrySessionLogState>(old, settings)!;

        old.Should().NotContain("Category");
        log.Errors.Single().Category.Should().BeNull();
        log.Errors.Single().Fingerprint.Should().Be(buffer.Errors.Items.Single().Fingerprint);
    }

    [Fact]
    public void GoesThroughTheJsonOfTheStates()
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());
        buffer.Apply(TelemetryBatchParser.Parse(Body(_message, "Game.Foo:Bar ()", "error")), _now);

        var settings = JsonStateSettings.CreateBase();
        var json = JsonConvert.SerializeObject(TelemetrySessionLogState.Of(buffer), settings);
        var log = JsonConvert.DeserializeObject<TelemetrySessionLogState>(json, settings)!;

        log.Errors.Single().Fingerprint.Should().Be(buffer.Errors.Items.Single().Fingerprint);
    }

    private static TelemetryStoredError Row(string message, string stack, string level = "exception", string? category = null)
    {
        return Rows(Body(message, stack, level, category)).Single();
    }

    private static IReadOnlyList<TelemetryStoredError> Rows(SharedClientTelemetry.Batch batch)
    {
        var buffer = TelemetrySessionBuffer.Empty(new ClientTelemetryStorageOptions());

        buffer.Apply(TelemetryBatchParser.Parse(batch), _now);

        return buffer.Errors.Items;
    }

    private static SharedClientTelemetry.Batch Body(string message, string stack, string level, string? category = null)
    {
        var wire = level == "error" ? SharedClientTelemetry.ErrorLevel.Error : SharedClientTelemetry.ErrorLevel.Exception;

        return TelemetryWire.Batch(Guid.NewGuid(), errors: [TelemetryWire.Error(1, wire, message, stack, category: category)]);
    }
}
