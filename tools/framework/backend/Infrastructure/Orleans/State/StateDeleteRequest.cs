using Npgsql;

namespace Infrastructure.State;

public class StateDeleteRequest
{
	public required IReadOnlyList<StateIdentity> Identities { get; init; }
	public NpgsqlTransaction? Transaction { get; init; }
}
