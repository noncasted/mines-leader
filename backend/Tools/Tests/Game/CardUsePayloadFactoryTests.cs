using FluentAssertions;
using Shared;
using Xunit;

namespace Tests.Game;

public class CardUsePayloadFactoryTests
{
    [Fact]
    public void Bloodhound_RequiresPosition()
    {
        var act = () => CardUsePayloadFactory.Create(CardType.Bloodhound, null, null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*position required*");
    }

    [Fact]
    public void Medic_DoesNotRequirePosition()
    {
        var payload = CardUsePayloadFactory.Create(CardType.Medic, null, null, null);

        payload.Should().BeOfType<CardUsePayload.Medic>();
        payload.Type.Should().Be(CardType.Medic);
    }

    [Fact]
    public void Recycler_RequiresExtraId()
    {
        var act = () => CardUsePayloadFactory.Create(CardType.Recycler, null, null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*extraCardId required*");
    }

    [Fact]
    public void Recycler_SetsDiscardCardId()
    {
        var extraId = Guid.NewGuid();

        var payload = CardUsePayloadFactory.Create(CardType.Recycler, null, extraId, null);

        payload.Should().BeOfType<CardUsePayload.Recycler>()
            .Which.DiscardCardId.Should().Be(extraId);
        payload.Type.Should().Be(CardType.Recycler);
    }

    [Fact]
    public void Bloodhound_SetsPosition()
    {
        var payload = CardUsePayloadFactory.Create(CardType.Bloodhound, new Position(2, 3), null, null);

        payload.Should().BeOfType<CardUsePayload.Bloodhound>()
            .Which.Position.Should().Be(new Position(2, 3));
        payload.Type.Should().Be(CardType.Bloodhound);
    }

    [Fact]
    public void Create_AllCardTypes_DoNotThrowWhenArgsPresent()
    {
        var extra = Guid.NewGuid();
        var position = new Position(0, 0);

        foreach (var type in CardTypeExtensions.All)
        {
            var act = () => CardUsePayloadFactory.Create(type, position, extra, 0);
            act.Should().NotThrow($"type {type} should be mapped");
        }
    }
}
