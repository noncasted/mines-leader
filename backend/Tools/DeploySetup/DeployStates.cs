using Benchmarks;
using Microsoft.Extensions.Configuration;
using Shared;

namespace DeploySetup;

// The states whose tables the migrator creates. Benchmark states (the Benchmarks assembly) get tables only where
// the benchmarks run (Benchmarks:Enabled, the dev cluster): a production database has none of them.
public static class DeployStates
{
    // For hosts without a reference to the Benchmarks assembly (the AppHost, the tests).
    public const string BenchmarksEnabledKey = BenchmarksAvailability.EnabledKey;

    // Every state of the composition, as the migrator sees it. Assemblies with their own generated StatesLookup
    // (the tests) read the migrator's list through this.
    public static IReadOnlyList<StatesLookup.Info> All => StatesLookup.All;

    public static IReadOnlyList<StatesLookup.Info> Select(IConfiguration configuration)
    {
        return Select(BenchmarksAvailability.ReadFlag(configuration));
    }

    public static IReadOnlyList<StatesLookup.Info> Select(bool benchmarksEnabled)
    {
        return StatesLookup.All
                           .Where(info => benchmarksEnabled == true || IsBenchmark(info) == false)
                           .ToList();
    }

    public static bool IsBenchmark(StatesLookup.Info info)
    {
        return info.Type.Assembly == BenchmarksAvailability.Assembly;
    }
}
