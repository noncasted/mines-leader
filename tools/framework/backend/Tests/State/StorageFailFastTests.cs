using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// Unexpected data of the storages throws instead of being skipped, rebuilt or replaced by a default.
[Collection(nameof(OrleansIntegrationCollection))]
public class StorageFailFastTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task ReadPage_StreamKeyNotOfKeyType_Throws()
    {
        var storage = GetSiloService<IEventStorage>();
        var prefix = $"event_test_{Guid.NewGuid():N}:";

        await storage.Append($"{prefix}not-a-guid", new CounterIncremented { Amount = 1 });

        var read = () => storage.ReadPage<Guid, EventTestAggregate>(prefix, Shared.GrainKeyType.Guid, 0, 10);

        await read.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task ReadBatch_DocumentNotEventState_Throws()
    {
        var store = GetSiloService<IDocumentStore>();
        var id = $"plain:{Guid.NewGuid()}";

        await using (var session = store.LightweightSession())
        {
            session.Store(new PlainDocument { Id = id });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var read = () => GetSiloService<IEventStorage>().ReadBatch<string, PlainDocument>([id]);

        await read.Should().ThrowAsync<InvalidCastException>();
    }

    [Fact]
    public async Task Read_SnapshotDoesNotDeserialize_Throws()
    {
        var storage = GetSiloService<IEventStorage>();
        var streamId = $"event_test:{Guid.NewGuid()}";

        await storage.Append(streamId, new CounterIncremented { Amount = 3 });

        await using (var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE mt_doc_eventtestaggregate SET data = '{\"Counter\":\"broken\"}'::jsonb WHERE id = @id";
            command.Parameters.AddWithValue("id", streamId);
            (await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        }

        var read = () => storage.Read<EventTestAggregate>(streamId);

        await read.Should().ThrowAsync<Exception>("a broken snapshot is not silently rebuilt from the events");
    }

    [Fact]
    public async Task Write_EventTypeMovedToAnotherAssembly_Resolved()
    {
        var storage = GetSiloService<IEventStorage>();
        var serializer = GetSiloService<IStateSerializer>();
        var streamId = $"event_test:{Guid.NewGuid()}";

        // Written while the type lived in another assembly: the namespace and the name are kept.
        var payload = new EventPayload
        {
            Type = $"{typeof(CounterIncremented).FullName}, Framework.OldGameAssembly",
            Json = serializer.Serialize(new CounterIncremented { Amount = 4 })
        };

        await using (var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await storage.Write(transaction, [new GrainEventRecord { StreamId = streamId, Events = [payload] }]);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        (await storage.Read<EventTestAggregate>(streamId)).Counter.Should().Be(4);
    }

    [Fact]
    public void BuildWriteCommands_SameStateTwice_Throws()
    {
        var storage = GetSiloService<IStateStorage>();
        var grainId = GrainId.Create(GrainType.Create("test"), GrainIdKeyExtensions.CreateGuidKey(Guid.NewGuid()));

        var records = new List<GrainStateRecord>
        {
            new() { Id = grainId, Value = TxTestState.Of(1) },
            new() { Id = grainId, Value = TxTestState.Of(2) }
        };

        var build = () => storage.BuildWriteCommands(records);

        build.Should().Throw<InvalidOperationException>().WithMessage("*twice*");
    }

    [Fact]
    public void StorageCache_SameTypeInTwoTables_EachQueryHasItsTable()
    {
        var cache = new StateStorageCache();

        var first = new StateIdentity { Key = 1, Type = "shared_type", TableName = "table_a", Extension = null };
        var second = new StateIdentity { Key = 1, Type = "shared_type", TableName = "table_b", Extension = null };

        cache.GetReadQuery(first).Should().Contain("table_a");
        cache.GetReadQuery(second).Should().Contain("table_b");
        cache.GetReadBatchQuery(first).Should().Contain("table_a");
        cache.GetReadBatchQuery(second).Should().Contain("table_b");
    }

    [Fact]
    public async Task Join_ConfigValueMissing_Throws()
    {
        var config = Substitute.For<ITransactionConfig>();
        config.Value.Returns((TransactionOptions)null!);
        var handler = new GrainTransactionHandler(config, NullLogger<GrainTransactionHandler>.Instance);

        await handler.Join(Guid.NewGuid());
        var join = () => handler.Join(Guid.NewGuid());

        // A missing config is not replaced by the defaults (a 3 s wait for the lock).
        (await join.Should().ThrowAsync<Exception>()).Which.Message.Should().NotContain("Handler failed to join");
    }

    public class PlainDocument
    {
        public string Id { get; init; } = string.Empty;
    }
}
