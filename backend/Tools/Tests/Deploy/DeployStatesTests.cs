using DeploySetup;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Tests.Deploy;

/// <summary>
/// F2.40: the migrator creates benchmark state tables only where the benchmarks run (Benchmarks:Enabled).
/// </summary>
public class DeployStatesTests
{
    [Fact]
    public void Select_BenchmarksOff_DropsOnlyBenchmarkStates()
    {
        var all = DeployStates.All;
        var selected = DeployStates.Select(false);

        all.Should().Contain(info => DeployStates.IsBenchmark(info), "the composition references the benchmarks");
        selected.Should().NotContain(info => info.Type.Assembly.GetName().Name == "Benchmarks");
        selected.Should().Equal(all.Where(info => DeployStates.IsBenchmark(info) == false));
        selected.Select(info => info.TableName).Should().NotContain("state_benchmark");
    }

    [Fact]
    public void Select_BenchmarksOn_KeepsEveryState()
    {
        DeployStates.Select(true).Should().Equal(DeployStates.All);
    }

    [Fact]
    public void Select_FlagNotSet_IsOff()
    {
        DeployStates.Select(Configuration(null)).Should().Equal(DeployStates.Select(false));
    }

    [Fact]
    public void Select_FlagSet_IsOn()
    {
        DeployStates.Select(Configuration("true")).Should().Equal(DeployStates.Select(true));
    }

    [Fact]
    public void Select_InvalidFlag_Throws()
    {
        var act = () => DeployStates.Select(Configuration("yes"));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{DeployStates.BenchmarksEnabledKey}*");
    }

    private static IConfiguration Configuration(string? enabled)
    {
        return new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string?> { [DeployStates.BenchmarksEnabledKey] = enabled })
               .Build();
    }
}
