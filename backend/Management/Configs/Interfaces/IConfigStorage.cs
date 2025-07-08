using Common;

namespace Management.Configs;

public interface IConfigStorage : IGrainWithStringKey
{
    Task<T> Get<T>();
    Task Set<T>(T value);
}

public static class ConfigStorageExtensions
{
    public static IConfigStorage GetConfigStorage<T>(this IOrleans orleans)
    {
        var key = typeof(T).Name!;
        return orleans.GetGrain<IConfigStorage>(key);
    }
    
    public static Task<T> GetConfig<T>(this IOrleans orleans)
    {
        var storage = orleans.GetConfigStorage<T>();
        return storage.Get<T>();
    }
    
    public static Task SetConfig<T>(this IOrleans orleans, T value)
    {
        var storage = orleans.GetConfigStorage<T>();
        return storage.Set<T>(value);
    }
}
