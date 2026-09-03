using FluentAssertions;
using Game.Session;
using Shared;
using Xunit;

namespace Tests.Game;

/// <summary>
/// Каталог должен покрывать каждый CardType: добавили карту без описания — тест падает.
/// </summary>
public class AgentCardCatalogTests
{
    public static TheoryData<CardType> AllTypes()
    {
        var data = new TheoryData<CardType>();
        foreach (var type in CardTypeExtensions.All)
            data.Add(type);

        return data;
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void EveryCardType_HasSummaryAndKnownShape(CardType type)
    {
        AgentCardCatalog.All.Should().ContainKey(type);

        var info = AgentCardCatalog.Get(type);

        info.Summary.Should().NotBeNullOrWhiteSpace();
        info.Summary.Should().NotBe("Unknown card");
        AgentCardCatalog.Shapes.Should().Contain(info.Shape);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void PositionCards_HaveTargetRule_AndOthersHaveNone(CardType type)
    {
        var needsPosition = CardUsePayloadFactory.CreateDefault(type) is IBoardCardUsePayload;
        var info = AgentCardCatalog.Get(type);

        if (needsPosition)
            info.Shape.Should().NotBe(AgentCardCatalog.ShapeNone, $"{type} needs a position, so legal plays need a rule");
        else
            info.Shape.Should().Be(AgentCardCatalog.ShapeNone, $"{type} has no position payload");
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Size_IsPositiveForPatterns_AndZeroOtherwise(CardType type)
    {
        CardConfigs.All.All.TryGetValue(type, out var config).Should().BeTrue($"{type} must have a config");

        var size = AgentCardCatalog.ResolveSize(type, config!);

        var shape = AgentCardCatalog.Get(type).Shape;
        if (shape == AgentCardCatalog.ShapeNone || shape == AgentCardCatalog.ShapeSingle)
            size.Should().Be(0);
        else
            size.Should().BePositive($"{type} pattern needs a size from config");
    }

    [Fact]
    public void MaxVariants_ShareBaseSummary()
    {
        AgentCardCatalog.Get(CardType.Trebuchet_Max).Summary
                        .Should().Be(AgentCardCatalog.Get(CardType.Trebuchet).Summary);
        AgentCardCatalog.Get(CardType.Bloodhound_Max).Summary
                        .Should().Be(AgentCardCatalog.Get(CardType.Bloodhound).Summary);
    }

    [Fact]
    public void RandomSizes_ReportMaximum()
    {
        var configs = CardConfigs.All;

        AgentCardCatalog.ResolveSize(CardType.ChaosDiamond, configs.ChaosDiamond_Normal)
                        .Should().Be(configs.ChaosDiamond_Normal.MaxSize);
        AgentCardCatalog.ResolveSize(CardType.ChaosScout, configs.ChaosScout_Normal)
                        .Should().Be(configs.ChaosScout_Normal.MaxLength);
        AgentCardCatalog.ResolveSize(CardType.CarpetBomb, configs.CarpetBomb_Normal)
                        .Should().Be(configs.CarpetBomb_Normal.Length);
        AgentCardCatalog.ResolveSize(CardType.ChainReaction, configs.ChainReaction_Normal)
                        .Should().Be(configs.ChainReaction_Normal.SpawnSize);
    }
}
