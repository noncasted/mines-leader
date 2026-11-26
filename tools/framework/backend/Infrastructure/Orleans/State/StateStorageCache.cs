using System.Collections.Concurrent;

namespace Infrastructure.State;

public class StateStorageCache
{
    // The text of a query names the table: one state name may live in several tables.
    private static (string TableName, string Type) Key(StateIdentity identity) => (identity.TableName, identity.Type);

    private readonly ConcurrentDictionary<(string TableName, string Type), string> _readQueries = new();
    private readonly ConcurrentDictionary<(string TableName, string Type), string> _readExtQueries = new();
    private readonly ConcurrentDictionary<(string TableName, string Type), string> _readAllQueries = new();
    private readonly ConcurrentDictionary<(string TableName, string Type), string> _readBatchQueries = new();
    private readonly ConcurrentDictionary<(string TableName, string Type), string> _readBatchExtQueries = new();

    public string GetReadQuery(StateIdentity stateIdentity)
    {
        var hasExtension = stateIdentity.Extension != null;
        var cache = hasExtension ? _readExtQueries : _readQueries;

        if (cache.TryGetValue(Key(stateIdentity), out var query))
            return query;

        var extensionClause = hasExtension ? "and extension = @extension" : string.Empty;

        query = $@"
            select value, version
            from {stateIdentity.TableName}
            where key = @key
            and type = @type
            {extensionClause}
            ";

        cache[Key(stateIdentity)] = query;
        return query;
    }

    public string GetReadAllQuery(StateIdentity stateIdentity)
    {
        if (_readAllQueries.TryGetValue(Key(stateIdentity), out var query))
            return query;

        query = $"SELECT key, value, version FROM {stateIdentity.TableName} WHERE type = @type";

        _readAllQueries[Key(stateIdentity)] = query;
        return query;
    }

    public string GetReadBatchQuery(StateIdentity stateIdentity)
    {
        var hasExtension = stateIdentity.Extension != null;
        var cache = hasExtension ? _readBatchExtQueries : _readBatchQueries;

        if (cache.TryGetValue(Key(stateIdentity), out var query))
            return query;

        var extensionClause = hasExtension ? "AND extension = @extension" : string.Empty;

        query =
            $"SELECT key, value, version FROM {stateIdentity.TableName} WHERE type = @type AND key = ANY(@keys) {extensionClause}";

        cache[Key(stateIdentity)] = query;
        return query;
    }
}
