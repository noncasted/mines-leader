using FluentAssertions;
using Game.ClientTelemetry;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The window of the summary of a closed session reads its log once, whatever the number of pages it holds.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetrySummaryWindowTests : ClientTelemetryEndpointTest
{
    public ClientTelemetrySummaryWindowTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task TheWindowOfAClosedSession_ReadsItsLogOnce()
    {
        var host = await StartHost();
        var session = Guid.NewGuid();

        // Two batches of single samples: more than one page of the reader would need a second read of the log.
        await Store(
            Request(session, 1, ["frame.dt"], [(1000, [10]), (2000, [20])]),
            Request(session, 2, samples: [(3000, [30])], ended: true));

        var before = Cluster.Storage.ReadsOf(session);
        var window = await Get(host, $"/sessions/{session}/summary?from=1500&to=3500");

        window.GetProperty("samples").GetInt32().Should().Be(2);
        (Cluster.Storage.ReadsOf(session) - before).Should().Be(1, "the log is read once, the row comes from the collection");
    }
}
