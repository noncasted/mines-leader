using Common.Extensions;

namespace Infrastructure.State;

public class GrainStateRecord
{
    public required GrainId Id { get; init; }
    public required IStateValue Value { get; init; }
}
