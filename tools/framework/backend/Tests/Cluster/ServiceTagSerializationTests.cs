using Cluster.Coordination;
using Cluster.Discovery;
using FluentAssertions;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans.Serialization;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Cluster;

// Overviews travel between services by Orleans serialization (discovery storage grain, messaging).
[Collection(nameof(OrleansIntegrationCollection))]
public class ServiceTagSerializationTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public void Orleans_RoundTrip_KeepsTag()
    {
        var serializer = GetSiloService<Serializer>();
        var overview = new GameServerOverview
        {
            Id = Guid.NewGuid(),
            Tag = TestRoles.Game,
            UpdateTime = DateTime.UtcNow,
            Url = "http://game"
        };

        var copy = serializer.Deserialize<IServiceOverview>(serializer.SerializeToArray<IServiceOverview>(overview));

        copy.Should().BeOfType<GameServerOverview>();
        copy.Tag.Should().Be(TestRoles.Game);
        new ServiceTag("Custom").Should().Be(
            serializer.Deserialize<ServiceTag>(serializer.SerializeToArray(new ServiceTag("Custom"))));
    }
}
