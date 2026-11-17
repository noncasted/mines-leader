using Npgsql;

namespace Infrastructure.State;

public class StateWriteRequest
{
    public required IReadOnlyDictionary<StateIdentity, IStateValue> Records { get; init; }
    public NpgsqlTransaction? Transaction { get; init; }
}
