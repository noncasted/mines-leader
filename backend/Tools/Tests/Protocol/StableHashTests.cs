using FluentAssertions;
using Game.Session;
using Xunit;

namespace Tests.Protocol;

/// <summary>
/// Shared.StableHash is part of the wire: entity property ids are StableHash of the value type full name, service
/// ids are StableHash of the service key. string.GetHashCode is randomized per process on CoreCLR (the backend and
/// the Unity 7000 client), so the values are pinned: FNV-1a over UTF-16 code units, byte for byte.
/// </summary>
public class StableHashTests
{
    [Theory]
    [InlineData("", -2128831035)]
    [InlineData("a", -468965076)]
    [InlineData("Location", 1539345862)]
    [InlineData("Shared.LobbyState", -2012294037)]
    [InlineData("Shared.PlayerLookState", -405933663)]
    [InlineData("Шрифт", 13425553)]
    public void Get_ReturnsKnownValue(string value, int expected)
    {
        Shared.StableHash.Get(value).Should().Be(expected);
    }

    // Always negative (the sign bit set): a service never takes the id of an entity.
    [Fact]
    public void ServiceId_IsStableHashOfKey()
    {
        new Service("Location").Id.Should().Be(1539345862 | int.MinValue);
    }
}
