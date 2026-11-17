using Common.Extensions;
using Internal;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.State;

public class DirectStorage
{
    public DirectStorage(
        IGrainStatesRegistry statesRegistry,
        IDbSource dbSource,
        IStateSerializer serializer,
        IStateMigrations migrations,
        ILogger<DirectStorage> logger)
    {
        Registry = statesRegistry;
        _commands = new DirectStateCommands(serializer);
        _reader = new DirectStateReader(statesRegistry, dbSource, serializer, migrations, logger);
        _writer = new DirectStateWriter(dbSource, _commands, logger);
    }

    private readonly DirectStateCommands _commands;
    private readonly DirectStateReader _reader;
    private readonly DirectStateWriter _writer;

    public IGrainStatesRegistry Registry { get; }

    public Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new() => _reader.Read<T>(identity);

    public Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        return _reader.ReadBatch<TKey, TValue>(identities);
    }

    public IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TValue : IStateValue, new()
    {
        return _reader.ReadAll<TKey, TValue>(lifetime);
    }

    public IAsyncEnumerable<TKey> ReadAllKeys<TKey>(GrainStateInfo stateInfo, IReadOnlyLifetime lifetime) =>
        _reader.ReadAllKeys<TKey>(stateInfo, lifetime);

    public Task<string> ReadRawJson(StateIdentity identity) => _reader.ReadRawJson(identity);

    public Task Write(StateWriteRequest request) => _writer.Write(request);

    public Task Delete(StateDeleteRequest request) => _writer.Delete(request);

    public IReadOnlyList<NpgsqlBatchCommand> BuildWriteCommands(IReadOnlyDictionary<StateIdentity, IStateValue> records) =>
        _commands.BuildWriteCommands(records);

    public static IReadOnlyList<NpgsqlBatchCommand> BuildDeleteCommands(IReadOnlyList<StateIdentity> identities) =>
        DirectStateCommands.BuildDeleteCommands(identities);
}
