using Cluster.Configs;
using FluentAssertions;
using Game.GamePlay.CardPreviews;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta;

// The previews are generated in the local setup of the gateway, before it takes a socket: the first connection after
// a deploy does not pay for them.
[Collection(nameof(OrleansIntegrationCollection))]
public class CardPreviewGeneratorTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public void Bundles_BeforeLocalSetup_Throws()
    {
        var generator = Generator();

        var read = () => generator.Bundles;

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task LocalSetup_GeneratesTheBundles()
    {
        var generator = Generator();
        var lifetime = new Lifetime();

        await generator.OnLocalSetupCompleted(lifetime);

        generator.Bundles.Should().NotBeEmpty();
        lifetime.Terminate();
    }

    private CardPreviewGenerator Generator()
    {
        return new CardPreviewGenerator(GetSiloService<ICardConfigs>(), NullLogger<CardPreviewGenerator>.Instance);
    }
}
