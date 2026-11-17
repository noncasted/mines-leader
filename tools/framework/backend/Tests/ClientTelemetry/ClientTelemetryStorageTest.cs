using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure;
using Internal;
using Npgsql;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// A real cluster and database shared by the classes of the collection (they run one by one): a test works with its own sessions
// and the parser stands in front of them, so what reaches the grain is what a gateway will pass. The connections of a test live on
// its lifetime, ended in DisposeAsync, so none of them stays in the shared silo after it.
public abstract class ClientTelemetryStorageTest : IAsyncLifetime
{
    protected ClientTelemetryStorageTest(ClientTelemetryClusterFixture cluster)
    {
        Cluster = cluster;
    }

    private readonly Lifetime _lifetime = new();

    protected ClientTelemetryClusterFixture Cluster { get; }
    protected IClientTelemetryReader Reader => Cluster.Reader;
    protected ILifetime TestLifetime => _lifetime;

    public ValueTask InitializeAsync()
    {
        Cluster.Storage.ResetFailures();
        return ValueTask.CompletedTask;
    }

    public virtual ValueTask DisposeAsync()
    {
        _lifetime.Terminate();
        Cluster.Storage.FailWrites(false);
        return ValueTask.CompletedTask;
    }

    // Through the grain, as ClientTelemetryConnection does. The first step starts the session, the guid is the one of the test.
    protected async Task Store(params TelemetryStep[] steps)
    {
        foreach (var step in steps)
        {
            var grain = Cluster.Orleans.GetGrain<IClientTelemetrySession>(step.Id);

            if (step.IsFirst)
                await grain.Start(step.Device, step.Tags, null, null);

            await grain.Accept(step.Batch);

            if (step.Ended)
                await grain.Close(TelemetrySessionStatus.Ended);
        }
    }

    protected async Task<TelemetrySessionInfo> Session(Guid id)
    {
        return await Reader.GetSession(id) ?? throw new InvalidOperationException($"No session {id}");
    }

    protected async Task<long> Scalar(string sql)
    {
        await using var connection = await Cluster.Database.DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    protected async Task<string?> RawState(string table, Guid id)
    {
        await using var connection = await Cluster.Database.DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand($"SELECT value::text FROM {table} WHERE key = '{id}'", connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;
    }

    protected static TelemetryStep Request(
        Guid session,
        long seq,
        string[]? names = null,
        (long Time, double?[] Values)[]? samples = null,
        (long Time, string Name)[]? marks = null,
        bool ended = false,
        string platform = "web",
        SharedClientTelemetry.Error[]? errors = null,
        string[]? tags = null)
    {
        return Step(session, seq, ended, TelemetryWire.Batch(session, names, samples?.Select(sample => (sample.Time, sample.Values, false)).ToArray(), errors, marks), platform, tags);
    }

    protected static TelemetryStep Step(Guid session, long seq, bool ended, SharedClientTelemetry.Batch batch, string platform = "web", string[]? tags = null)
    {
        return new TelemetryStep(
            session,
            seq == 1,
            ended,
            TelemetryDeviceParser.Parse(new SharedClientTelemetry.Device { Platform = platform }),
            TelemetryTagsParser.Parse(tags),
            TelemetryBatchParser.Parse(batch));
    }

    protected sealed record TelemetryStep(Guid Id, bool IsFirst, bool Ended, TelemetryDevice Device, TelemetrySessionTags Tags, TelemetryBatch Batch);
}
