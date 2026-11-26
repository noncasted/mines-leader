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

public class ServiceTagTests
{

    [Fact]
    public void Tags_WithSameValue_AreEqual()
    {
        var custom = new ServiceTag("Game");

        (custom == TestRoles.Game).Should().BeTrue();
        custom.GetHashCode().Should().Be(TestRoles.Game.GetHashCode());
        (new ServiceTag("Matchmaker") == TestRoles.Game).Should().BeFalse();
        new HashSet<ServiceTag> { TestRoles.Meta, new("Meta") }.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_Empty_Throws(string value)
    {
        var act = () => new ServiceTag(value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Default_Value_Throws()
    {
        var act = () => default(ServiceTag).Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Json_WritesString_ReadsStringAndLegacyEnumNumbers()
    {
        var settings = JsonStateSettings.CreateBase();
        var overview = new ServiceOverview { Id = Guid.NewGuid(), Tag = TestRoles.Game, UpdateTime = DateTime.UtcNow };

        var json = JsonConvert.SerializeObject(overview, settings);

        json.Should().Contain("\"Tag\":\"Game\"");
        JsonConvert.DeserializeObject<ServiceOverview>(json, settings)!.Tag.Should().Be(TestRoles.Game);

        foreach (var (number, tag) in new[] { (100, "Coordinator"), (200, "Meta"), (300, "Game"), (400, "Silo"), (500, "Console") })
        {
            var legacy = json.Replace("\"Tag\":\"Game\"", $"\"Tag\":{number}");
            JsonConvert.DeserializeObject<ServiceOverview>(legacy, settings)!.Tag.Should().Be(new ServiceTag(tag));
        }

        var unknown = json.Replace("\"Tag\":\"Game\"", "\"Tag\":700");
        var act = () => JsonConvert.DeserializeObject<ServiceOverview>(unknown, settings);
        act.Should().Throw<JsonSerializationException>();
    }

    // There is no default set: the composition root configures it (ConfigureRequiredServices).
    [Fact]
    public void StartupConfig_NotConfigured_Throws()
    {
        var act = () => new ClusterStartupConfig(Options.Create(new ClusterStartupOptions()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ConfigureRequiredServices*");
    }

    [Fact]
    public void StartupConfig_ConfigureRequiredServices_ReplacesTheSet()
    {
        var matchmaker = new ServiceTag("Matchmaker");
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.ConfigureRequiredServices(ServiceTag.Coordinator, matchmaker);
        builder.Services.AddSingleton<IClusterStartupConfig, ClusterStartupConfig>();

        using var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<IClusterStartupConfig>().RequiredServices
                .Should().Equal(ServiceTag.Coordinator, matchmaker);
    }

    [Fact]
    public void PublishGameServerUrl_MarksTheOptions()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.PublishGameServerUrl();

        using var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value.PublishGameServerUrl.Should().BeTrue();
    }

    [Fact]
    public void StartupConfig_EmptyOrDuplicates_Throws()
    {
        var empty = () => new ClusterStartupConfig(Options.Create(new ClusterStartupOptions { RequiredServices = [] }));
        var duplicates = () => new ClusterStartupConfig(Options.Create(new ClusterStartupOptions
        {
            RequiredServices = [ServiceTag.Coordinator, new ServiceTag("Coordinator")]
        }));

        empty.Should().Throw<InvalidOperationException>();
        duplicates.Should().Throw<InvalidOperationException>();
    }
}
