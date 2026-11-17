using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Common.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Framework.Tests.Common;

// The deploy setup waits for a database that is still starting (a transient failure), but a failure no retry can fix
// (a wrong connection string, a refused password) ends at once with its own exception.
public class DbConnectRetryTests
{
    [Fact]
    public async Task GetConnection_PermanentFailure_ThrowsAtOnceWithTheCause()
    {
        var configuration = Configuration("NoSuchKeyword=1", attempts: 3, retryDelayMs: 1000);
        var stopwatch = Stopwatch.StartNew();

        await FluentActions.Awaiting(() => configuration.GetConnection()).Should().ThrowAsync<ArgumentException>();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(900));
    }

    [Fact]
    public async Task GetConnection_TransientFailure_RetriesEveryAttemptThenThrowsTheLastCause()
    {
        // A server that accepts and drops every connection: the client sees the end of the stream, a transient failure.
        // Without SSL and GSS encryption an attempt is one connection (Prefer tries again without them).
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = 0;
        var accepting = Accept();
        var configuration = Configuration($"Host=127.0.0.1;Port={port};Timeout=5;Pooling=false;SslMode=Disable;GssEncryptionMode=Disable", attempts: 3, retryDelayMs: 10);

        var failure = await FluentActions.Awaiting(() => configuration.GetConnection()).Should().ThrowAsync<NpgsqlException>();

        failure.Which.IsTransient.Should().BeTrue();
        Volatile.Read(ref accepted).Should().Be(3);
        listener.Stop();
        await FluentActions.Awaiting(() => accepting).Should().ThrowAsync<Exception>();

        async Task Accept()
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
                // Counted before the drop, so the client throws only after its attempt is counted.
                Interlocked.Increment(ref accepted);
            }
        }
    }

    private static ConfigurationManager Configuration(string connectionString, int attempts, int retryDelayMs)
    {
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["postgres"] = connectionString,
            ["PostgresConnect:Attempts"] = attempts.ToString(),
            ["PostgresConnect:RetryDelayMs"] = retryDelayMs.ToString()
        });

        return configuration;
    }
}
