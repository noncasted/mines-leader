using Npgsql;

namespace Infrastructure.State;

public interface IGrainStateStorage
{
    Task<T> Load<T>(GrainAddress address) where T : class, new();
    Task Save<T>(GrainAddress address);
}

public class GrainStateStorage : IGrainStateStorage
{
    public GrainStateStorage(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    private readonly NpgsqlDataSource _dataSource;
    
    public Task<T> Load<T>(GrainAddress address) where T : class, new()
    {
        return default;
    }

    public Task Save<T>(GrainAddress address)
    {
        return default;
    }
}