using Common.Extensions;
using System.Text.RegularExpressions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.State.Storages;

// The direct storage by its public surface: every key type, extensions, the transaction rules of a write, the text
// of its SQL and what it logs on a failure.
[Collection(nameof(OrleansIntegrationCollection))]
public class DirectStorageTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private const string MissingTable = "state_direct_missing";

    private DirectStorage Storage => GetSiloService<DirectStorage>();

    [Fact]
    public async Task Read_NothingStored_ReturnsNewValue()
    {
        var value = await Storage.Read<DirectGuidState>(Identity<DirectGuidState>(Guid.NewGuid()));

        value.Value.Should().Be(0);
    }

    [Fact]
    public async Task Write_SameIdentityTwice_KeepsLastValue()
    {
        var identity = Identity<DirectGuidState>(Guid.NewGuid());

        await Write((identity, DirectGuidState.Of(1)));
        await Write((identity, DirectGuidState.Of(2)));

        (await Storage.Read<DirectGuidState>(identity)).Value.Should().Be(2);
    }

    [Fact]
    public async Task Read_WithExtension_ReadsOnlyThatExtension()
    {
        var key = Guid.NewGuid();
        var first = Identity<DirectGuidExtState>(key, "first");
        var second = Identity<DirectGuidExtState>(key, "second");

        await Write((first, DirectGuidExtState.Of(1)), (second, DirectGuidExtState.Of(2)));

        (await Storage.Read<DirectGuidExtState>(first)).Value.Should().Be(1);
        (await Storage.Read<DirectGuidExtState>(second)).Value.Should().Be(2);
        (await Storage.Read<DirectGuidExtState>(Identity<DirectGuidExtState>(key, "third"))).Value.Should().Be(0);
    }

    [Fact]
    public async Task ReadBatch_Empty_ReturnsEmpty()
    {
        (await Storage.ReadBatch<Guid, DirectGuidState>([])).Should().BeEmpty();
    }

    [Fact]
    public async Task ReadBatch_GuidKeys_ReturnsStored()
    {
        var stored = Identity<DirectGuidState>(Guid.NewGuid());
        await Write((stored, DirectGuidState.Of(5)));

        var result = await Storage.ReadBatch<Guid, DirectGuidState>([stored, Identity<DirectGuidState>(Guid.NewGuid())]);

        result.Should().ContainSingle();
        result[(Guid)stored.Key].Value.Should().Be(5);
    }

    [Fact]
    public async Task ReadBatch_StringKeys_ReturnsStored()
    {
        var stored = Identity<DirectStringState>("a-key");
        await Write((stored, DirectStringState.Of(6)));

        var result = await Storage.ReadBatch<string, DirectStringState>([stored]);

        result["a-key"].Value.Should().Be(6);
    }

    [Fact]
    public async Task ReadBatch_IntegerKeys_ReturnsStored()
    {
        var stored = Identity<DirectIntegerState>(77L);
        await Write((stored, DirectIntegerState.Of(7)));

        var result = await Storage.ReadBatch<long, DirectIntegerState>([stored]);

        result[77L].Value.Should().Be(7);
    }

    [Fact]
    public async Task ReadBatch_TwoExtensions_EachGroupReadsItsOwn()
    {
        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();

        await Write(
            (Identity<DirectGuidExtState>(firstKey, "a"), DirectGuidExtState.Of(1)),
            (Identity<DirectGuidExtState>(firstKey, "other"), DirectGuidExtState.Of(100)),
            (Identity<DirectGuidExtState>(secondKey, "b"), DirectGuidExtState.Of(2)));

        var result = await Storage.ReadBatch<Guid, DirectGuidExtState>(
        [
            Identity<DirectGuidExtState>(firstKey, "a"),
            Identity<DirectGuidExtState>(secondKey, "b")
        ]);

        result.Should().HaveCount(2);
        result[firstKey].Value.Should().Be(1);
        result[secondKey].Value.Should().Be(2);
    }

    [Fact]
    public async Task ReadBatch_IntegerAndExtensionKeys_ReturnsStored()
    {
        await Write((Identity<DirectIntegerExtState>(9L, "x"), DirectIntegerExtState.Of(9)));

        var result = await Storage.ReadBatch<long, DirectIntegerExtState>([Identity<DirectIntegerExtState>(9L, "x")]);

        result[9L].Value.Should().Be(9);
    }

    [Fact]
    public async Task ReadBatch_KeyTypeNotSupported_Throws()
    {
        var registry = Substitute.For<IGrainStatesRegistry>();

        registry.Get<DirectGuidState>().Returns(new GrainStateInfo
        {
            TableName = "state_direct_guid",
            KeyType = (GrainKeyType)99,
            Type = typeof(DirectGuidState),
            Name = "direct_guid"
        });

        var logger = new RecordingLogger<DirectStorage>();
        var storage = Create(logger, registry);

        var read = () => storage.ReadBatch<Guid, DirectGuidState>([Identity<DirectGuidState>(Guid.NewGuid())]);

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("?DirectStorage] Unsupported key type: 99");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("Failed to batch read DirectGuidState count=1"));
    }

    [Fact]
    public async Task ReadAll_StringKeys_ReturnsStored()
    {
        await Write((Identity<DirectStringState>("all-key"), DirectStringState.Of(3)));

        var result = await ToList(Storage.ReadAll<string, DirectStringState>(new Lifetime(TestLifetime)));

        result.Should().ContainSingle();
        result[0].Item1.Should().Be("all-key");
        result[0].Item2.Value.Should().Be(3);
    }

    [Fact]
    public async Task ReadAll_IntegerKeys_ReturnsStored()
    {
        await Write((Identity<DirectIntegerState>(31L), DirectIntegerState.Of(4)));

        var result = await ToList(Storage.ReadAll<long, DirectIntegerState>(new Lifetime(TestLifetime)));

        result.Should().ContainSingle();
        result[0].Item1.Should().Be(31L);
        result[0].Item2.Value.Should().Be(4);
    }

    [Fact]
    public async Task ReadAllKeys_EveryKeyType_ReturnsStoredKeys()
    {
        var guid = Guid.NewGuid();

        await Write(
            (Identity<DirectGuidState>(guid), DirectGuidState.Of(1)),
            (Identity<DirectStringState>("key"), DirectStringState.Of(1)),
            (Identity<DirectIntegerState>(5L), DirectIntegerState.Of(1)));

        var registry = Storage.Registry;

        (await ToList(Storage.ReadAllKeys<Guid>(registry.Get<DirectGuidState>(), new Lifetime(TestLifetime)))).Should().Equal(guid);
        (await ToList(Storage.ReadAllKeys<string>(registry.Get<DirectStringState>(), new Lifetime(TestLifetime)))).Should().Equal("key");
        (await ToList(Storage.ReadAllKeys<long>(registry.Get<DirectIntegerState>(), new Lifetime(TestLifetime)))).Should().Equal(5L);
    }

    [Fact]
    public async Task ReadAllKeys_KeyTypeNotSupported_Throws()
    {
        await Write((Identity<DirectGuidState>(Guid.NewGuid()), DirectGuidState.Of(1)));

        var info = new GrainStateInfo
        {
            TableName = "state_direct_guid",
            KeyType = (GrainKeyType)99,
            Type = typeof(DirectGuidState),
            Name = "direct_guid"
        };

        var read = () => ToList(Storage.ReadAllKeys<Guid>(info, new Lifetime(TestLifetime)));

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("?DirectStorage] Unsupported key type: 99");
    }

    [Fact]
    public async Task ReadAll_LifetimeTerminated_Cancels()
    {
        var lifetime = new Lifetime(TestLifetime);
        lifetime.Terminate();

        var read = () => ToList(Storage.ReadAll<Guid, DirectGuidState>(lifetime));

        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadRawJson_Stored_ReturnsJsonOfValue()
    {
        var identity = Identity<DirectGuidState>(Guid.NewGuid());
        await Write((identity, DirectGuidState.Of(12)));

        var raw = await Storage.ReadRawJson(identity);

        raw.Should().StartWith("{").And.Contain("\"Value\": 12");
    }

    [Fact]
    public async Task ReadRawJson_NothingStored_ReturnsEmpty()
    {
        (await Storage.ReadRawJson(Identity<DirectGuidState>(Guid.NewGuid()))).Should().BeEmpty();
    }

    [Fact]
    public async Task Write_TwoTablesNoTransaction_FailureOfOneRollsBackBoth()
    {
        var kept = Identity<DirectGuidState>(Guid.NewGuid());
        var logger = new RecordingLogger<DirectStorage>();
        var storage = Create(logger);

        var missing = new StateIdentity { Key = Guid.NewGuid(), Type = "x", TableName = MissingTable, Extension = null };

        var write = () => storage.Write(new StateWriteRequest
        {
            Records = new Dictionary<StateIdentity, IStateValue>
            {
                [kept] = DirectGuidState.Of(1),
                [missing] = DirectGuidState.Of(2)
            }
        });

        await write.Should().ThrowAsync<Exception>();

        (await Storage.Read<DirectGuidState>(kept)).Value.Should().Be(0, "several tables are written in one transaction");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message == "[DirectStorage] Failed to write 2 records");
    }

    [Fact]
    public async Task Write_TwoTablesNoTransaction_WritesBoth()
    {
        var first = Identity<DirectGuidState>(Guid.NewGuid());
        var second = Identity<DirectStringState>("two-tables");

        await Write((first, DirectGuidState.Of(1)), (second, DirectStringState.Of(2)));

        (await Storage.Read<DirectGuidState>(first)).Value.Should().Be(1);
        (await Storage.Read<DirectStringState>(second)).Value.Should().Be(2);
    }

    [Fact]
    public async Task Write_NothingToWrite_DoesNothing()
    {
        var logger = new RecordingLogger<DirectStorage>();

        await Create(logger).Write(new StateWriteRequest { Records = new Dictionary<StateIdentity, IStateValue>() });

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Write_CallerTransactionRolledBack_NothingStored()
    {
        var identity = Identity<DirectGuidState>(Guid.NewGuid());

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.Write(new StateWriteRequest
        {
            Records = new Dictionary<StateIdentity, IStateValue> { [identity] = DirectGuidState.Of(1) },
            Transaction = transaction
        });

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        (await Storage.Read<DirectGuidState>(identity)).Value.Should().Be(0);
    }

    [Fact]
    public async Task Delete_NoIdentities_DoesNothing()
    {
        var logger = new RecordingLogger<DirectStorage>();

        await Create(logger).Delete(new StateDeleteRequest { Identities = [] });

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_WithExtension_DeletesOnlyThatExtension()
    {
        var key = Guid.NewGuid();
        var first = Identity<DirectGuidExtState>(key, "first");
        var second = Identity<DirectGuidExtState>(key, "second");

        await Write((first, DirectGuidExtState.Of(1)), (second, DirectGuidExtState.Of(2)));
        await Storage.Delete(new StateDeleteRequest { Identities = [first] });

        (await Storage.Read<DirectGuidExtState>(first)).Value.Should().Be(0);
        (await Storage.Read<DirectGuidExtState>(second)).Value.Should().Be(2);
    }

    [Fact]
    public async Task Delete_CallerTransactionRolledBack_KeepsValue()
    {
        var identity = Identity<DirectGuidState>(Guid.NewGuid());
        await Write((identity, DirectGuidState.Of(1)));

        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Storage.Delete(new StateDeleteRequest { Identities = [identity], Transaction = transaction });
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        (await Storage.Read<DirectGuidState>(identity)).Value.Should().Be(1);
    }

    [Fact]
    public async Task Delete_TableMissing_LogsAndThrows()
    {
        var logger = new RecordingLogger<DirectStorage>();

        var missing = new StateIdentity { Key = Guid.NewGuid(), Type = "x", TableName = MissingTable, Extension = null };
        var delete = () => Create(logger).Delete(new StateDeleteRequest { Identities = [missing] });

        await delete.Should().ThrowAsync<Exception>();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message == "[DirectStorage] Failed to delete 1 records");
    }

    [Fact]
    public async Task Read_TableMissing_LogsRawAndTypedFailureAndThrows()
    {
        var logger = new RecordingLogger<DirectStorage>();
        var key = Guid.NewGuid();

        var missing = new StateIdentity { Key = key, Type = "x", TableName = MissingTable, Extension = null };
        var read = () => Create(logger).Read<DirectGuidState>(missing);

        await read.Should().ThrowAsync<Exception>();

        logger.Entries.Select(e => e.Message).Should().Equal(
            $"[DirectStorage] Failed to read raw key={key} type=x",
            $"[DirectStorage] Failed to read DirectGuidState key={key} type=x");
    }

    [Fact]
    public async Task ReadBatch_TableMissing_LogsAndThrows()
    {
        var logger = new RecordingLogger<DirectStorage>();

        var missing = new StateIdentity { Key = Guid.NewGuid(), Type = "x", TableName = MissingTable, Extension = null };
        var read = () => Create(logger).ReadBatch<Guid, DirectGuidState>([missing]);

        await read.Should().ThrowAsync<Exception>();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message == "[DirectStorage] Failed to batch read DirectGuidState count=1");
    }

    [Fact]
    public async Task ReadBatch_OldVersionStored_Migrates()
    {
        var key = Guid.NewGuid().ToString();
        await GetGrain<IMigrationGrainV0>(key).Write(11);

        var result = await Storage.ReadBatch<string, MigrationTestState_1>([Identity<MigrationTestState_1>(key)]);

        result[key].Value.Should().Be(11);
        result[key].Label.Should().Be("migrated-11");
    }

    [Fact]
    public async Task ReadAll_OldVersionStored_Migrates()
    {
        var key = Guid.NewGuid().ToString();
        await GetGrain<IMigrationGrainV0>(key).Write(13);

        var result = await ToList(Storage.ReadAll<string, MigrationTestState_1>(new Lifetime(TestLifetime)));

        result.Should().ContainSingle(entry => entry.Item1 == key && entry.Item2.Label == "migrated-13");
    }

    // jsonb "null" passes NOT NULL and deserializes to nothing: Read and ReadBatch throw on it, ReadAll too.
    [Fact]
    public async Task ReadAll_JsonNullStored_Throws()
    {
        var identity = Identity<DirectGuidState>(Guid.NewGuid());
        await using var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = $"INSERT INTO {identity.TableName} (key, type, value, version) VALUES (@key, @type, 'null'::jsonb, 1000000)";
            insert.Parameters.AddWithValue("key", identity.Key);
            insert.Parameters.AddWithValue("type", identity.Type);
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            var read = () => ToList(Storage.ReadAll<Guid, DirectGuidState>(new Lifetime(TestLifetime)));

            await read.Should().ThrowAsync<Exception>();
        }
        finally
        {
            await using var delete = connection.CreateCommand();
            delete.CommandText = $"DELETE FROM {identity.TableName} WHERE key = @key AND type = @type";
            delete.Parameters.AddWithValue("key", identity.Key);
            delete.Parameters.AddWithValue("type", identity.Type);
            await delete.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void Registry_IsTheGivenOne()
    {
        var registry = Substitute.For<IGrainStatesRegistry>();

        Create(new RecordingLogger<DirectStorage>(), registry).Registry.Should().BeSameAs(registry);
    }

    [Fact]
    public void BuildWriteCommands_GroupsByTableAndExtensionInOrderOfRecords()
    {
        var plainKey = Guid.NewGuid();
        var extKey = Guid.NewGuid();

        var commands = Storage.BuildWriteCommands(new Dictionary<StateIdentity, IStateValue>
        {
            [Identity<DirectGuidExtState>(extKey, "e")] = DirectGuidExtState.Of(1),
            [Identity<DirectGuidState>(plainKey)] = DirectGuidState.Of(2),
            [Identity<DirectGuidState>(Guid.NewGuid())] = DirectGuidState.Of(3)
        });

        commands.Should().HaveCount(2);

        Normalized(commands[0].CommandText).Should().Be(
            "INSERT INTO state_direct_guid_ext (key, type, version, value, extension) " +
            "VALUES (@key0, @type0, @version0, @value0::jsonb, @ext0) " +
            "ON CONFLICT (key, type, extension) DO UPDATE SET value = EXCLUDED.value, version = EXCLUDED.version");

        Normalized(commands[1].CommandText).Should().Be(
            "INSERT INTO state_direct_guid (key, type, version, value) " +
            "VALUES (@key0, @type0, @version0, @value0::jsonb), (@key1, @type1, @version1, @value1::jsonb) " +
            "ON CONFLICT (key, type) DO UPDATE SET value = EXCLUDED.value, version = EXCLUDED.version");

        commands[0].Parameters.Select(p => p.ParameterName).Should().Equal("key0", "type0", "version0", "value0", "ext0");
        commands[0].Parameters["value0"].NpgsqlDbType.Should().Be(NpgsqlDbType.Jsonb);
        commands[1].Parameters["key0"].Value.Should().Be(plainKey);
        commands[1].Parameters["type1"].Value.Should().Be("direct_guid");
    }

    [Fact]
    public void BuildDeleteCommands_GroupsByTableAndExtension()
    {
        var commands = DirectStorage.BuildDeleteCommands(
        [
            Identity<DirectGuidState>(Guid.NewGuid()),
            Identity<DirectGuidExtState>(Guid.NewGuid(), "e"),
            Identity<DirectGuidState>(Guid.NewGuid())
        ]);

        commands.Should().HaveCount(2);

        commands[0].CommandText.Should().Be(
            "DELETE FROM state_direct_guid WHERE (key = @key0 AND type = @type0) OR (key = @key1 AND type = @type1)");

        commands[1].CommandText.Should().Be(
            "DELETE FROM state_direct_guid_ext WHERE (key = @key0 AND type = @type0 AND extension = @ext0)");

        commands[1].Parameters.Select(p => p.ParameterName).Should().Equal("key0", "type0", "ext0");
    }

    [Fact]
    public void BuildWriteCommands_Empty_NoCommands()
    {
        Storage.BuildWriteCommands(new Dictionary<StateIdentity, IStateValue>()).Should().BeEmpty();
        DirectStorage.BuildDeleteCommands([]).Should().BeEmpty();
    }

    private DirectStorage Create(RecordingLogger<DirectStorage> logger, IGrainStatesRegistry? registry = null)
    {
        return new DirectStorage(
            registry ?? GetSiloService<IGrainStatesRegistry>(),
            GetSiloService<IDbSource>(),
            GetSiloService<IStateSerializer>(),
            GetSiloService<IStateMigrations>(),
            logger);
    }

    private Task Write(params (StateIdentity Identity, IStateValue Value)[] records)
    {
        return Storage.Write(new StateWriteRequest
        {
            Records = records.ToDictionary(record => record.Identity, record => record.Value)
        });
    }

    private StateIdentity Identity<T>(object key, string? extension = null)
    {
        var info = GetSiloService<IGrainStatesRegistry>().Get<T>();

        return new StateIdentity { Key = key, Type = info.Name, TableName = info.TableName, Extension = extension };
    }

    private static async Task<List<T>> ToList<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();

        await foreach (var item in source)
            result.Add(item);

        return result;
    }

    private static string Normalized(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();
}
