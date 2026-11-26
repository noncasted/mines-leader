using Common.Extensions;
using System.Text.RegularExpressions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State.Storages;

// The side effects storage by its public surface: the moves of an entry between its tables and the monitor
// operations of the retry queue.
[Collection(nameof(SideEffectIntegrationCollection))]
public class SideEffectsStorageTests(SideEffectTestFixture fixture)
    : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    private ISideEffectsStorage Storage => GetSiloService<ISideEffectsStorage>();

    [Fact]
    public async Task WriteInTransaction_NoEffects_WritesNothing()
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.Write(transaction, []);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        (await Storage.GetStats()).QueueCount.Should().Be(0);
    }

    [Fact]
    public async Task WriteInTransaction_RolledBack_WritesNothing()
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.Write(transaction, [new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() }]);
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        (await Storage.GetStats()).QueueCount.Should().Be(0);
    }

    [Fact]
    public async Task Write_SerializerFails_LogsAndThrowsAndWritesNothing()
    {
        var serializer = Substitute.For<IStateSerializer>();
        serializer.Serialize(Arg.Any<ISideEffect>()).Returns<string>(_ => throw new InvalidOperationException("no json"));

        var logger = new RecordingLogger<SideEffectsStorage>();
        var storage = new SideEffectsStorage(GetSiloService<IDbSource>(), serializer, logger);

        var write = () => storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });

        await write.Should().ThrowAsync<InvalidOperationException>().WithMessage("no json");

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Error && e.Message == "[SideEffectsStorage] Failed to write side effect");

        (await Storage.GetStats()).QueueCount.Should().Be(0);
    }

    [Fact]
    public void BuildWriteCommand_TextAndParameters()
    {
        var command = Storage.BuildWriteCommand(
        [
            new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() },
            new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() }
        ]);

        Normalized(command.CommandText).Should().Be(
            "INSERT INTO side_effects_queue (id, payload, retry_count, created_at) " +
            "VALUES (@id0, @payload0::jsonb, 0, now()), (@id1, @payload1::jsonb, 0, now())");

        command.Parameters.Select(p => p.ParameterName).Should().Equal("id0", "payload0", "id1", "payload1");
        command.Parameters["payload1"].NpgsqlDbType.Should().Be(NpgsqlDbType.Jsonb);
    }

    [Fact]
    public async Task Read_TakesOldestUpToCount_MovesToProcessing()
    {
        var old = Guid.NewGuid();
        var young = Guid.NewGuid();

        await InsertQueue(old, Payload(), DateTime.UtcNow.AddMinutes(-2), retryCount: 3);
        await InsertQueue(young, Payload(), DateTime.UtcNow.AddMinutes(-1), retryCount: 0);

        var entries = await Storage.Read(1);

        entries.Should().ContainSingle();
        entries[0].Id.Should().Be(old);
        entries[0].RetryCount.Should().Be(3);
        entries[0].Effect.Should().BeOfType<AlwaysFailingSideEffect>();

        var stats = await Storage.GetStats();
        stats.QueueCount.Should().Be(1);
        stats.ProcessingCount.Should().Be(1);
    }

    [Fact]
    public async Task Read_BrokenPayload_ReturnsEntryThatFailsWithTheError()
    {
        var id = Guid.NewGuid();
        await InsertQueue(id, """{"$type":"No.Such.Type, NoAssembly"}""", DateTime.UtcNow, retryCount: 2);

        var logger = new RecordingLogger<SideEffectsStorage>();
        var storage = new SideEffectsStorage(GetSiloService<IDbSource>(), GetSiloService<IStateSerializer>(), logger);

        var entry = (await storage.Read(1)).Single();

        entry.Id.Should().Be(id);
        entry.RetryCount.Should().Be(2);

        var execute = () => entry.Effect.Execute(null!);
        await execute.Should().ThrowAsync<InvalidOperationException>().WithMessage("?SideEffects] Dead letter: failed to deserialize payload: *");

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Error && e.Message == $"[SideEffectsStorage] Failed to deserialize side effect {id}");
    }

    [Fact]
    public async Task FailProcessing_BelowMax_MovesToRetryWithGrowingDelay()
    {
        var entry = await WriteAndRead();
        var before = DateTime.UtcNow;

        await Storage.FailProcessing(entry.Id, retryCount: 1, maxAttempts: 5, incrementalRetryDelaySeconds: 100f, "ignored");

        var stats = await Storage.GetStats();
        stats.ProcessingCount.Should().Be(0);
        stats.RetryCount.Should().Be(1);
        stats.DeadLetterCount.Should().Be(0);

        var retry = (await Storage.GetRetryEntries(10)).Single();
        retry.Id.Should().Be(entry.Id);
        retry.RetryCount.Should().Be(2);
        retry.RetryAfter.Should().BeCloseTo(before.AddSeconds(200), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task FailProcessing_AtMax_MovesToDeadLetterWithError()
    {
        var entry = await WriteAndRead();
        var logger = new RecordingLogger<SideEffectsStorage>();
        var storage = new SideEffectsStorage(GetSiloService<IDbSource>(), GetSiloService<IStateSerializer>(), logger);

        await storage.FailProcessing(entry.Id, retryCount: 4, maxAttempts: 5, incrementalRetryDelaySeconds: 1f, "the error");

        var stats = await Storage.GetStats();
        stats.ProcessingCount.Should().Be(0);
        stats.RetryCount.Should().Be(0);
        stats.DeadLetterCount.Should().Be(1);

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT error_message FROM side_effects_dead_letter WHERE id = @id";
        command.Parameters.AddWithValue("id", entry.Id);
        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().Be("the error");

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Error
            && e.Message == $"[SideEffects] Effect {entry.Id} moved to dead letter after 5 attempts: the error");
    }

    [Fact]
    public async Task FailProcessing_AtMaxWithoutError_StoresNull()
    {
        var entry = await WriteAndRead();

        await Storage.FailProcessing(entry.Id, retryCount: 4, maxAttempts: 5, incrementalRetryDelaySeconds: 1f);

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT error_message FROM side_effects_dead_letter WHERE id = @id";
        command.Parameters.AddWithValue("id", entry.Id);
        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task CompleteProcessing_InTransactionRolledBack_KeepsEntry()
    {
        var entry = await WriteAndRead();

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.CompleteProcessing(transaction, entry.Id);
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        (await Storage.GetStats()).ProcessingCount.Should().Be(1);
    }

    [Fact]
    public async Task CompleteProcessing_Standalone_DeletesEntry()
    {
        var entry = await WriteAndRead();

        await Storage.CompleteProcessing(entry.Id);

        (await Storage.GetStats()).ProcessingCount.Should().Be(0);
    }

    [Fact]
    public async Task RequeueStuckOlderThan_OldEntry_MovedAndLogged()
    {
        var entry = await WriteAndRead();
        await SetProcessingStart(entry.Id, DateTime.UtcNow.AddHours(-1));

        var logger = new RecordingLogger<SideEffectsStorage>();
        var storage = new SideEffectsStorage(GetSiloService<IDbSource>(), GetSiloService<IStateSerializer>(), logger);
        var age = TimeSpan.FromMinutes(5);

        await storage.RequeueStuckOlderThan(age);

        (await Storage.GetStats()).QueueCount.Should().Be(1);

        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning && e.Message == $"[SideEffectsStorage] Requeued 1 stuck entries older than {age}");
    }

    [Fact]
    public async Task RequeueStuckOlderThan_RecentEntry_StaysAndNoLog()
    {
        await WriteAndRead();

        var logger = new RecordingLogger<SideEffectsStorage>();
        var storage = new SideEffectsStorage(GetSiloService<IDbSource>(), GetSiloService<IStateSerializer>(), logger);

        await storage.RequeueStuckOlderThan(TimeSpan.FromMinutes(5));

        (await Storage.GetStats()).ProcessingCount.Should().Be(1);
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task TouchProcessing_OldEntry_NotTakenAsStuck()
    {
        var entry = await WriteAndRead();
        await SetProcessingStart(entry.Id, DateTime.UtcNow.AddHours(-1));

        await Storage.TouchProcessing([entry.Id]);
        await Storage.RequeueStuckOlderThan(TimeSpan.FromMinutes(5));

        (await Storage.GetStats()).ProcessingCount.Should().Be(1);
    }

    [Fact]
    public async Task TouchProcessing_NoIds_DoesNothing()
    {
        await Storage.TouchProcessing([]);

        (await Storage.GetStats()).ProcessingCount.Should().Be(0);
    }

    [Fact]
    public async Task RequeueReady_TakesOnlyReadyEntries()
    {
        var ready = Guid.NewGuid();
        var later = Guid.NewGuid();

        await InsertRetry(ready, Payload(), DateTime.UtcNow.AddMinutes(-1), retryCount: 4);
        await InsertRetry(later, Payload(), DateTime.UtcNow.AddHours(1), retryCount: 1);

        await Storage.RequeueReady();

        var entry = (await Storage.Read(10)).Single();
        entry.Id.Should().Be(ready);
        entry.RetryCount.Should().Be(4);
        (await Storage.GetRetryEntries(10)).Single().Id.Should().Be(later);
    }

    [Fact]
    public async Task GetStats_CountsEveryTable()
    {
        await WriteAndRead();
        var dead = await WriteAndRead();
        await Storage.FailProcessing(dead.Id, 1, 1, 1f, "dead");
        await Storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
        await InsertRetry(Guid.NewGuid(), Payload(), DateTime.UtcNow.AddHours(1), 1);

        var stats = await Storage.GetStats();

        stats.QueueCount.Should().Be(1);
        stats.ProcessingCount.Should().Be(1);
        stats.RetryCount.Should().Be(1);
        stats.DeadLetterCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRetryEntries_OrderedByRetryAfterUpToLimit()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        await InsertRetry(second, Payload(), DateTime.UtcNow.AddHours(2), 2, created);
        await InsertRetry(first, Payload(), DateTime.UtcNow.AddHours(1), 7, created);
        await InsertRetry(third, Payload(), DateTime.UtcNow.AddHours(3), 3, created);

        var entries = await Storage.GetRetryEntries(2);

        entries.Select(e => e.Id).Should().Equal(first, second);
        entries[0].RetryCount.Should().Be(7);
        entries[0].CreatedAt.Should().Be(created);
    }

    [Theory]
    [InlineData("""{"$type":"Some.Name.Space.Effect"}""", "Effect")]
    [InlineData("""{"$type":"Plain"}""", "Plain")]
    [InlineData("""{"other":1}""", "Unknown")]
    [InlineData("""{"$type":1}""", "Unknown")]
    [InlineData("""[1]""", "Unknown")]
    [InlineData("""{"$type":"Some.Name.Space.Effect, Some.Assembly"}""", "Effect")]
    [InlineData("""{"$type":"Some.Outer+Effect, Some.Assembly"}""", "Outer+Effect")]
    [InlineData("""{"$type":"Some.Box`1[[Some.Item, Some.Assembly]], Some.Assembly"}""", "Box`1")]
    [InlineData("""{"a":{"$type":"Some.Nested"},"$type":"Some.Effect, Some.Assembly"}""", "Effect")]
    public async Task GetRetryEntries_TypeNameFromPayload(string payload, string expected)
    {
        await InsertRetry(Guid.NewGuid(), payload, DateTime.UtcNow.AddHours(1), 0);

        (await Storage.GetRetryEntries(10)).Single().TypeName.Should().Be(expected);
    }

    // "$type" of a serialized effect holds the assembly too (TypeNameAssemblyFormatHandling.Simple).
    [Fact]
    public async Task GetRetryEntries_RealEffect_ShortTypeName()
    {
        var entry = await WriteAndRead();
        await Storage.FailProcessing(entry.Id, 0, 5, 1000f);

        (await Storage.GetRetryEntries(10)).Single().TypeName.Should().Be(nameof(AlwaysFailingSideEffect));
    }

    [Fact]
    public async Task DropRetryEntry_DeletesOnlyThatEntry()
    {
        var dropped = Guid.NewGuid();
        var kept = Guid.NewGuid();

        await InsertRetry(dropped, Payload(), DateTime.UtcNow.AddHours(1), 1);
        await InsertRetry(kept, Payload(), DateTime.UtcNow.AddHours(1), 1);

        await Storage.DropRetryEntry(dropped);

        (await Storage.GetRetryEntries(10)).Single().Id.Should().Be(kept);
        (await Storage.GetStats()).QueueCount.Should().Be(0);
    }

    [Fact]
    public async Task RequeueRetryEntry_MovesOnlyThatEntryWithItsCount()
    {
        var moved = Guid.NewGuid();
        var kept = Guid.NewGuid();

        await InsertRetry(moved, Payload(), DateTime.UtcNow.AddHours(1), 6);
        await InsertRetry(kept, Payload(), DateTime.UtcNow.AddHours(1), 1);

        await Storage.RequeueRetryEntry(moved);

        (await Storage.GetRetryEntries(10)).Single().Id.Should().Be(kept);

        var entry = (await Storage.Read(10)).Single();
        entry.Id.Should().Be(moved);
        entry.RetryCount.Should().Be(6);
    }

    [Fact]
    public async Task RequeueRetryEntry_AlreadyInQueue_DropsFromRetryKeepsQueued()
    {
        var id = Guid.NewGuid();

        await InsertQueue(id, Payload(), DateTime.UtcNow, retryCount: 9);
        await InsertRetry(id, Payload(), DateTime.UtcNow.AddHours(1), 1);

        await Storage.RequeueRetryEntry(id);

        (await Storage.GetRetryEntries(10)).Should().BeEmpty();
        (await Storage.Read(10)).Single().RetryCount.Should().Be(9);
    }

    private async Task<SideEffectEntry> WriteAndRead()
    {
        await Storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
        return (await Storage.Read(1)).Single();
    }

    private string Payload()
    {
        return GetSiloService<IStateSerializer>().Serialize<ISideEffect>(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
    }

    private Task InsertQueue(Guid id, string payload, DateTime created, int retryCount)
    {
        return Execute(
            "INSERT INTO side_effects_queue (id, payload, retry_count, created_at) VALUES (@id, @payload::jsonb, @retry, @created)",
            command => {
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("payload", payload);
                command.Parameters.AddWithValue("retry", retryCount);
                command.Parameters.AddWithValue("created", created);
            });
    }

    private Task InsertRetry(Guid id, string payload, DateTime retryAfter, int retryCount, DateTime? created = null)
    {
        return Execute(
            "INSERT INTO side_effects_retry_queue (id, payload, retry_count, created_at, retry_after) " +
            "VALUES (@id, @payload::jsonb, @retry, @created, @after)",
            command => {
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("payload", payload);
                command.Parameters.AddWithValue("retry", retryCount);
                command.Parameters.AddWithValue("created", created ?? DateTime.UtcNow);
                command.Parameters.AddWithValue("after", retryAfter);
            });
    }

    private Task SetProcessingStart(Guid id, DateTime start)
    {
        return Execute(
            "UPDATE side_effects_processing SET processing_started_at = @start WHERE id = @id",
            command => {
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("start", start);
            });
    }

    private async Task Execute(string sql, Action<NpgsqlCommand> parameters)
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        parameters(command);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string Normalized(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();
}
