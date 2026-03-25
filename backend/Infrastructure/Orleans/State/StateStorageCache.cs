namespace Infrastructure.State;

public class StateStorageCache
{
    public StateStorageCache(IGrainStatesRegistry statesRegistry)
    {
        _statesRegistry = statesRegistry;
    }

    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly Dictionary<Type, string> _readQueries = new();
    private readonly Dictionary<Type, string> _readExtQueries = new();
    private readonly Dictionary<Type, string> _writeQueries = new();
    private readonly Dictionary<Type, string> _writeExtQueries = new();

    public string GetReadQuery<T>(bool hasExtension) where T : IStateValue, new()
    {
        var cache = hasExtension ? _readExtQueries : _readQueries;
        var key = typeof(T);

        if (cache.TryGetValue(key, out var query))
            return query;

        var stateInfo = _statesRegistry.Get<T>();
        var extensionClause = hasExtension ? "and extension = @extension" : string.Empty;

        query = $@"
            select value, version
            from {stateInfo.TableName}
            where key = @key
            and type = @type
            {extensionClause}
            ";

        cache[key] = query;
        return query;
    }

    public string GetWriteQuery(Type type, bool hasExtension)
    {
        var cache = hasExtension ? _writeExtQueries : _writeQueries;

        if (cache.TryGetValue(type, out var query))
            return query;

        var stateInfo = _statesRegistry.Get(type);
        var extension = hasExtension ? ", extension" : string.Empty;
        var extensionParam = hasExtension ? ", @extension" : string.Empty;

        query = $@"
            insert into {stateInfo.TableName}
            (key, type, version, value{extension})
            values (@key, @type, @version, @value::jsonb{extensionParam})
            on conflict (key, type{extension})
            do update set value = EXCLUDED.value
            ";

        cache[type] = query;
        return query;
    }
}
