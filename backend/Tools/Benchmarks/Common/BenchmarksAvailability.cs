using System.Net;
using System.Reflection;
using Common.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Benchmarks;

// Whether this host runs the benchmarks (Benchmarks:Enabled). Registered in every host, also when they are off,
// so that the console can show that they are off.
public class BenchmarksAvailability
{
    public BenchmarksAvailability(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    // Opt-in flag, environment variable Benchmarks__Enabled. The dev AppHost (Aspire) sets it.
    public const string EnabledKey = "Benchmarks:Enabled";

    public bool IsEnabled { get; }

    // The assembly with the benchmark grains, nodes and states.
    public static Assembly Assembly => typeof(BenchmarksAvailability).Assembly;

    // Benchmarks write and delete state rows of their own and load the cluster: they run only in Development and
    // only against a local database. An enabled flag anywhere else fails the startup.
    public static BenchmarksAvailability Read(IConfiguration configuration, IHostEnvironment environment)
    {
        if (ReadFlag(configuration) == false)
            return new BenchmarksAvailability(false);

        if (environment.IsDevelopment() == false)
        {
            throw new InvalidOperationException(
                $"{EnabledKey} is set in the {environment.EnvironmentName} environment: benchmarks run only in Development");
        }

        var connectionString = configuration.GetConnectionString(ConnectionNames.Postgres);

        if (string.IsNullOrWhiteSpace(connectionString) == true)
            throw new InvalidOperationException($"{EnabledKey} is set, but the '{ConnectionNames.Postgres}' connection string is not");

        var host = new NpgsqlConnectionStringBuilder(connectionString).Host ?? "";

        if (IsLocalHost(host) == false)
        {
            throw new InvalidOperationException(
                $"{EnabledKey} is set, but the database host '{host}' is not local: benchmarks never run against a remote database");
        }

        return new BenchmarksAvailability(true);
    }

    // The raw flag without the environment checks: for the migrator, which creates the benchmark tables only
    // where the services run the benchmarks. Not set: off.
    public static bool ReadFlag(IConfiguration configuration)
    {
        var raw = configuration[EnabledKey];

        if (string.IsNullOrWhiteSpace(raw) == true)
            return false;

        if (bool.TryParse(raw, out var enabled) == false)
            throw new InvalidOperationException($"{EnabledKey} must be true or false, got '{raw}'");

        return enabled;
    }

    private static bool IsLocalHost(string hosts)
    {
        var entries = hosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (entries.Length == 0)
            return false;

        foreach (var entry in entries)
        {
            // Host[:port]; an IPv6 address may come in brackets.
            var name = entry;

            if (name.StartsWith('[') == true)
                name = name[1..name.IndexOf(']')];
            else if (name.Count(c => c == ':') == 1)
                name = name[..name.IndexOf(':')];

            if (string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase) == true)
                continue;

            if (IPAddress.TryParse(name, out var address) == false || IPAddress.IsLoopback(address) == false)
                return false;
        }

        return true;
    }
}
