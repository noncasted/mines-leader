using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orchestration;
using Orleans.Configuration;
using Xunit;

namespace Tests.Deploy;

/// <summary>
/// F2.11: ClusterId / ServiceId come from configuration and are required for the Orleans client and silo.
/// </summary>
public class OrleansClusterIdentityTests
{
    private const string Postgres = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none";

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Client_UsesConfiguredIdentity(string environment)
    {
        var builder = CreateBuilder(environment, ("Orleans:ClusterId", "game-a"), ("Orleans:ServiceId", "game-a-service"));

        builder.AddOrleansClient();

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<ClusterOptions>>().Value;

        options.ClusterId.Should().Be("game-a");
        options.ServiceId.Should().Be("game-a-service");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Silo_UsesConfiguredIdentity(string environment)
    {
        var builder = CreateBuilder(environment, ("Orleans:ClusterId", "game-b"), ("Orleans:ServiceId", "game-b-service"));

        builder.ConfigureSilo();

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<ClusterOptions>>().Value;

        options.ClusterId.Should().Be("game-b");
        options.ServiceId.Should().Be("game-b-service");
    }

    [Theory]
    [InlineData("Orleans:ClusterId")]
    [InlineData("Orleans:ServiceId")]
    public void Client_MissingIdentity_Throws(string missingKey)
    {
        var values = new[] { ("Orleans:ClusterId", "game-a"), ("Orleans:ServiceId", "game-a") }
                     .Where(pair => pair.Item1 != missingKey)
                     .ToArray();

        var builder = CreateBuilder(Environments.Development, values);

        var act = () => {
            builder.AddOrleansClient();
            builder.Build().Dispose();
        };

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{missingKey}*");
    }

    [Fact]
    public void Silo_EmptyIdentity_Throws()
    {
        var builder = CreateBuilder(Environments.Production, ("Orleans:ClusterId", " "), ("Orleans:ServiceId", "game"));

        var act = () => {
            builder.ConfigureSilo();
            builder.Build().Dispose();
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*Orleans:ClusterId*");
    }

    private static HostApplicationBuilder CreateBuilder(string environment, params (string Key, string Value)[] values)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            DisableDefaults = true
        });

        var settings = values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
        settings["ConnectionStrings:postgres"] = Postgres;
        builder.Configuration.AddInMemoryCollection(settings);

        return builder;
    }
}
