using Npgsql;

namespace Infrastructure;

public interface ISideEffectsStorage
{
    Task<IReadOnlyList<ISideEffect>> Read(int count);
    Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects);
}

public class SideEffectsStorage : ISideEffectsStorage
{
    public Task<IReadOnlyList<ISideEffect>> Read(int count)
    {
        throw new NotImplementedException();
    }

    public Task Write(NpgsqlTransaction transaction, IReadOnlyList<ISideEffect> effects)
    {
        throw new NotImplementedException();
    }
}