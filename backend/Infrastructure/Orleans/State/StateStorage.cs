using Common.Extensions;
using Common.Reactive;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.State;

public class StateIdentity
{
    public required object Key { get; init; }
    public required string Type { get; init; }
    public required string TableName { get; init; }
    public required string? Extension { get; init; }
}

public class GrainStateRecord
{
    public required GrainId Id { get; init; }
    public required IStateValue Value { get; init; }
}

public class StateWriteRequest
{
    public required IReadOnlyDictionary<StateIdentity, IStateValue> Records { get; init; }
    public NpgsqlTransaction? Transaction { get; init; }
}

public class StateDeleteRequest
{
	public required IReadOnlyList<StateIdentity> Identities { get; init; }
	public NpgsqlTransaction? Transaction { get; init; }
}

public interface IStateStorage
{
    IGrainStatesRegistry Registry { get; }

    Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new();

    Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : class, IStateValue, new();

    IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TKey : notnull
        where TValue : class, IStateValue, new();

    Task Write(StateWriteRequest request);

    // Builds the direct-state upsert commands without executing them, so a caller owning a transaction
    // (Transactions.Process) can put them into one NpgsqlBatch together with other writes.
    IReadOnlyList<NpgsqlBatchCommand> BuildWriteCommands(IReadOnlyDictionary<StateIdentity, IStateValue> records);

    Task Delete(StateDeleteRequest request);

    Task<string> ReadRawJson(StateIdentity identity);
}

public class StateStorage : IStateStorage
{
	public StateStorage(
		DirectStorage directStorage,
		IEventStorage eventStorage,
		IDocumentStore documentStore,
		IGrainStatesRegistry statesRegistry,
		IStateSerializer stateSerializer,
		IDbSource dbSource,
		ILogger<StateStorage> logger)
	{
		_directStorage = directStorage;
		_eventStorage = eventStorage;
		_documentStore = documentStore;
		_stateSerializer = stateSerializer;
		_dbSource = dbSource;
		_logger = logger;
		Registry = statesRegistry;
	}

	private readonly DirectStorage _directStorage;
	private readonly IEventStorage _eventStorage;
	private readonly IDocumentStore _documentStore;
	private readonly IStateSerializer _stateSerializer;
	private readonly IDbSource _dbSource;
	private readonly ILogger<StateStorage> _logger;

    public IGrainStatesRegistry Registry { get; }

    public Task<T> Read<T>(StateIdentity identity) where T : IStateValue, new()
    {
        if (typeof(T).IsAssignableTo(typeof(IDirectStateValue)))
            return _directStorage.Read<T>(identity);

        if (typeof(T).IsAssignableTo(typeof(IEventStateValue)))
        {
            var streamId = $"{identity.Type}:{identity.Key}";
            var method = typeof(IEventStorage).GetMethod("Read")!.MakeGenericMethod(typeof(T));
            return (Task<T>)method.Invoke(_eventStorage, [streamId])!;
        }

        throw new InvalidOperationException($"State type {typeof(T).Name} must implement either IDirectStateValue or IEventStateValue.");
    }

    public Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        if (typeof(TValue).IsAssignableTo(typeof(IDirectStateValue)))
            return _directStorage.ReadBatch<TKey, TValue>(identities);

        if (typeof(TValue).IsAssignableTo(typeof(IEventStateValue)))
            return ReadBatchEventSourced<TKey, TValue>(identities);

        throw new InvalidOperationException($"State type {typeof(TValue).Name} must implement either IDirectStateValue or IEventStateValue.");
    }

    private async Task<IReadOnlyDictionary<TKey, TValue>> ReadBatchEventSourced<TKey, TValue>(IReadOnlyList<StateIdentity> identities)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        if (identities.Count == 0)
            return new Dictionary<TKey, TValue>();

        var streamIds = identities.Select(i => $"{i.Type}:{i.Key}").ToList();
        var result = await _eventStorage.ReadBatch<TKey, TValue>(streamIds);

        return result;
    }

    public IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime)
        where TKey : notnull
        where TValue : class, IStateValue, new()
    {
        if (typeof(TValue).IsAssignableTo(typeof(IDirectStateValue)))
            return _directStorage.ReadAll<TKey, TValue>(lifetime);

        if (typeof(TValue).IsAssignableTo(typeof(IEventStateValue)))
        {
            var stateInfo = Registry.Get<TValue>();
            var prefix = stateInfo.Name + ":";
            return _eventStorage.ReadAll<TKey, TValue>(prefix, stateInfo.KeyType);
        }

        throw new InvalidOperationException($"State type {typeof(TValue).Name} must implement either IDirectStateValue or IEventStateValue.");
    }

    public async Task Write(StateWriteRequest request)
    {
        var directRecords = FilterDirect(request.Records);

        if (directRecords.Count != 0)
        {
            await _directStorage.Write(new StateWriteRequest
            {
                Records = directRecords,
                Transaction = request.Transaction
            });
        }
    }

    public IReadOnlyList<NpgsqlBatchCommand> BuildWriteCommands(IReadOnlyDictionary<StateIdentity, IStateValue> records)
    {
        var directRecords = FilterDirect(records);

        return directRecords.Count != 0
            ? _directStorage.BuildWriteCommands(directRecords)
            : [];
    }

    private static Dictionary<StateIdentity, IStateValue> FilterDirect(IReadOnlyDictionary<StateIdentity, IStateValue> records)
    {
        var directRecords = new Dictionary<StateIdentity, IStateValue>();

        foreach (var (identity, value) in records)
        {
            if (value is IDirectStateValue)
            {
                directRecords.Add(identity, value);
            }
            else if (value is IEventStateValue)
            {
                throw new NotSupportedException(
                    $"Writing event-sourced state of type {value.GetType().Name} through StateStorage is not supported. " +
                    "Event-sourced state must be written through IEventStorage.Append or EventState.Write.");
            }
            else
            {
                throw new InvalidOperationException($"State value type {value.GetType().Name} must implement either IDirectStateValue or IEventStateValue.");
            }
        }

        return directRecords;
    }

	public async Task Delete(StateDeleteRequest request)
	{
		var identities = request.Identities;

		if (identities.Count == 0)
			return;

		var eventStreamIds = new List<string>();
		var directIdentities = new List<StateIdentity>();

		foreach (var identity in identities)
		{
			var info = Registry.All.FirstOrDefault(s => s.Name == identity.Type);

			if (info != null && typeof(IEventStateValue).IsAssignableFrom(info.Type))
				eventStreamIds.Add($"{identity.Type}:{identity.Key}");
			else
				directIdentities.Add(identity);
		}

		if (eventStreamIds.Count == 0 && directIdentities.Count == 0)
			return;

		await using var connection = await _dbSource.Value.OpenConnectionAsync();
		await using var transaction = await connection.BeginTransactionAsync();

		try
		{
			if (eventStreamIds.Count != 0)
				await _eventStorage.DeleteBatch(transaction, eventStreamIds);

			if (directIdentities.Count != 0)
				await _directStorage.Delete(new StateDeleteRequest { Identities = directIdentities, Transaction = transaction });

			await transaction.CommitAsync();
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}
	}

    public async Task<string> ReadRawJson(StateIdentity identity)
    {
        var info = Registry.All.FirstOrDefault(s => s.Name == identity.Type);

        if (info != null && typeof(IEventStateValue).IsAssignableFrom(info.Type))
        {
            var streamId = $"{identity.Type}:{identity.Key}";
            var readMethod = typeof(IEventStorage).GetMethod("Read")!.MakeGenericMethod(info.Type);
            var task = (Task)readMethod.Invoke(_eventStorage, [streamId])!;
            await task;
            var resultProperty = task.GetType().GetProperty("Result")!;
            var aggregate = resultProperty.GetValue(task);
            if (aggregate == null)
                return "No state data";
            return _stateSerializer.Serialize(aggregate);
        }

        return await _directStorage.ReadRawJson(identity);
    }
}
