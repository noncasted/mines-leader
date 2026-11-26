using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Marten;
using Shared;
using Xunit;

namespace Framework.Tests.State.Storages;

// The event storage beyond EventStorageTests: every key type of the reads, batches of deletes, the order of a page and
// the failures of a write.
[Collection(nameof(OrleansIntegrationCollection))]
public class EventStorageSurfaceTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private IEventStorage Storage => GetSiloService<IEventStorage>();

    [Fact]
    public async Task Rebuild_NoStream_ReturnsNewValue()
    {
        (await Storage.Rebuild<EventTestAggregate>($"event_test:{Guid.NewGuid()}")).Counter.Should().Be(0);
    }

    [Fact]
    public async Task Rebuild_Stream_AggregatesEvents()
    {
        var streamId = $"event_test:{Guid.NewGuid()}";
        await Storage.Append(streamId, new CounterIncremented { Amount = 2 }, new CounterIncremented { Amount = 3 });

        (await Storage.Rebuild<EventTestAggregate>(streamId)).Counter.Should().Be(5);
    }

    [Fact]
    public async Task DeleteBatch_Empty_DoesNothing()
    {
        var streamId = $"event_test:{Guid.NewGuid()}";
        await Storage.Append(streamId, new CounterIncremented { Amount = 1 });

        await Storage.DeleteBatch([]);

        (await Storage.Read<EventTestAggregate>(streamId)).Counter.Should().Be(1);
    }

    [Fact]
    public async Task DeleteBatch_MoreThanOneBatch_ArchivesEveryStream()
    {
        var streams = await AppendStreams(1001);

        await Storage.DeleteBatch(streams);

        await ShouldBeArchived(streams);
    }

    [Fact]
    public async Task DeleteBatchInTransaction_MoreThanOneBatch_ArchivesOnCommit()
    {
        var streams = await AppendStreams(1001);

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.DeleteBatch(transaction, streams);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        await ShouldBeArchived(streams);
    }

    [Fact]
    public async Task DeleteBatchInTransaction_RolledBack_KeepsStreams()
    {
        var streamId = $"event_test:{Guid.NewGuid()}";
        await Storage.Append(streamId, new CounterIncremented { Amount = 1 });

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.DeleteBatch(transaction, [streamId]);
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        await using var session = GetSiloService<IDocumentStore>().QuerySession();
        var state = await session.Events.FetchStreamStateAsync(streamId, TestContext.Current.CancellationToken);
        state!.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBatchInTransaction_Empty_DoesNothing()
    {
        var streamId = $"event_test:{Guid.NewGuid()}";
        await Storage.Append(streamId, new CounterIncremented { Amount = 1 });

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.DeleteBatch(transaction, []);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        await using var session = GetSiloService<IDocumentStore>().QuerySession();
        var state = await session.Events.FetchStreamStateAsync(streamId, TestContext.Current.CancellationToken);
        state!.IsArchived.Should().BeFalse();
        (await Storage.Read<EventTestAggregate>(streamId)).Counter.Should().Be(1);
    }

    [Fact]
    public async Task ReadBatch_StringKeys_KeyIsTheRestOfStreamId()
    {
        var streamId = $"event_test:{Guid.NewGuid()}:tail";
        await Storage.Append(streamId, new CounterIncremented { Amount = 1 });

        var result = await Storage.ReadBatch<string, EventTestAggregate>([streamId]);

        result[streamId["event_test:".Length..]].Counter.Should().Be(1);
    }

    [Fact]
    public async Task ReadBatch_StreamIdWithoutColon_KeyIsWholeId()
    {
        var streamId = Guid.NewGuid().ToString();
        await Storage.Append(streamId, new CounterIncremented { Amount = 1 });

        var result = await Storage.ReadBatch<string, EventTestAggregate>([streamId]);

        result[streamId].Counter.Should().Be(1);
    }

    [Fact]
    public async Task ReadBatch_LongKeys_Parsed()
    {
        var key = Random.Shared.NextInt64(1, long.MaxValue);
        var streamId = $"event_test:{key}";
        await Storage.Append(streamId, new CounterIncremented { Amount = 1 });

        (await Storage.ReadBatch<long, EventTestAggregate>([streamId]))[key].Counter.Should().Be(1);
    }

    [Fact]
    public async Task ReadBatch_KeyTypeNotSupported_Throws()
    {
        var streamId = "event_test:15";
        await Storage.Append(streamId, new CounterIncremented { Amount = 1 });

        var read = () => Storage.ReadBatch<int, EventTestAggregate>([streamId]);

        await read.Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("?EventStorage] Unsupported batch key type: Int32");
    }

    [Fact]
    public async Task ReadAll_StringAndIntegerKeys_Parsed()
    {
        var prefix = $"event_{Guid.NewGuid():N}:";
        await Storage.Append($"{prefix}15", new CounterIncremented { Amount = 1 });

        (await ToList(Storage.ReadAll<string, EventTestAggregate>(prefix, GrainKeyType.String))).Single().Key.Should().Be("15");
        (await ToList(Storage.ReadAll<long, EventTestAggregate>(prefix, GrainKeyType.Integer))).Single().Key.Should().Be(15L);
        (await ToList(Storage.ReadAll<long, EventTestAggregate>(prefix, GrainKeyType.IntegerAndString))).Single().Key.Should().Be(15L);
    }

    [Fact]
    public async Task ReadAll_KeyTypeNotSupported_Throws()
    {
        var prefix = $"event_{Guid.NewGuid():N}:";
        await Storage.Append($"{prefix}15", new CounterIncremented { Amount = 1 });

        var read = () => ToList(Storage.ReadAll<long, EventTestAggregate>(prefix, (GrainKeyType)99));

        await read.Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("?EventStorage] Unsupported key type: 99");
    }

    [Fact]
    public async Task ReadPage_Ascending_OrderedAndPaged()
    {
        var prefix = $"event_{Guid.NewGuid():N}:";

        foreach (var (key, amount) in new[] { ("a", 3), ("b", 1), ("c", 2) })
            await Storage.Append($"{prefix}{key}", new CounterIncremented { Amount = amount });

        var page = await Storage.ReadPage<string, EventTestAggregate>(
            prefix, GrainKeyType.String, offset: 1, limit: 1, nameof(EventTestAggregate.Counter), descending: false);

        page.TotalCount.Should().Be(3);
        page.Entries.Single().Key.Should().Be("c");
    }

    [Fact]
    public async Task ReadPage_Descending_OrderedByProperty()
    {
        var prefix = $"event_{Guid.NewGuid():N}:";

        foreach (var (key, amount) in new[] { ("a", 3), ("b", 1), ("c", 2) })
            await Storage.Append($"{prefix}{key}", new CounterIncremented { Amount = amount });

        var page = await Storage.ReadPage<string, EventTestAggregate>(
            prefix, GrainKeyType.String, offset: 0, limit: 10, nameof(EventTestAggregate.Counter));

        page.Entries.Select(e => e.Key).Should().Equal("a", "c", "b");
    }

    [Theory]
    [InlineData("NoSuchProperty", false)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public async Task ReadPage_NoSuchProperty_OrderedById(string? property, bool descending)
    {
        var prefix = $"event_{Guid.NewGuid():N}:";

        foreach (var key in new[] { "b", "a", "c" })
            await Storage.Append($"{prefix}{key}", new CounterIncremented { Amount = 1 });

        var page = await Storage.ReadPage<string, EventTestAggregate>(
            prefix, GrainKeyType.String, offset: 0, limit: 10, property, descending);

        page.Entries.Select(e => e.Key).Should().Equal(descending ? ["c", "b", "a"] : ["a", "b", "c"]);
    }

    [Fact]
    public async Task Write_RecordWithoutEvents_AppendsNothing()
    {
        var streamId = $"event_test:{Guid.NewGuid()}";

        await WriteInTransaction(new GrainEventRecord { StreamId = streamId, Events = [] });

        await using var session = GetSiloService<IDocumentStore>().QuerySession();
        (await session.Events.FetchStreamAsync(streamId, token: TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Write_EventJsonBroken_ThrowsWithTypeAndJson()
    {
        var payload = new EventPayload { Type = typeof(CounterIncremented).AssemblyQualifiedName!, Json = "{broken" };

        var write = () => WriteInTransaction(new GrainEventRecord { StreamId = $"event_test:{Guid.NewGuid()}", Events = [payload] });

        (await write.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Failed to deserialize event of type 'CounterIncremented'. Json: {broken")
            .WithInnerException<Exception>();
    }

    [Fact]
    public async Task Write_EventJsonNull_ThrowsWrappedError()
    {
        var payload = new EventPayload { Type = typeof(CounterIncremented).AssemblyQualifiedName!, Json = "null" };

        var write = () => WriteInTransaction(new GrainEventRecord { StreamId = $"event_test:{Guid.NewGuid()}", Events = [payload] });

        (await write.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Failed to deserialize event of type 'CounterIncremented'. Json: null")
            .WithInnerException<InvalidOperationException>()
            .WithMessage("Event deserialization returned null. Type: CounterIncremented, Json: null");
    }

    [Fact]
    public async Task Write_RolledBack_AppendsNothing()
    {
        var streamId = $"event_test:{Guid.NewGuid()}";
        var serializer = GetSiloService<IStateSerializer>();

        var payload = new EventPayload
        {
            Type = typeof(CounterIncremented).AssemblyQualifiedName!,
            Json = serializer.Serialize(new CounterIncremented { Amount = 4 })
        };

        await using (var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await Storage.Write(transaction, [new GrainEventRecord { StreamId = streamId, Events = [payload] }]);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        (await Storage.Read<EventTestAggregate>(streamId)).Counter.Should().Be(0);
    }

    private async Task<List<string>> AppendStreams(int count)
    {
        var streams = Enumerable.Range(0, count).Select(_ => $"event_test:{Guid.NewGuid()}").ToList();

        await using var session = GetSiloService<IDocumentStore>().LightweightSession();

        foreach (var streamId in streams)
            session.Events.Append(streamId, new CounterIncremented { Amount = 1 });

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return streams;
    }

    private async Task ShouldBeArchived(IReadOnlyList<string> streams)
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM mt_streams WHERE id = ANY(@ids) AND is_archived = true";
        command.Parameters.AddWithValue("ids", streams.ToArray());

        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().Be((long)streams.Count);
    }

    private async Task WriteInTransaction(GrainEventRecord record)
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.Write(transaction, [record]);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<T>> ToList<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();

        await foreach (var item in source)
            result.Add(item);

        return result;
    }
}
