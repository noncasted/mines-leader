using Shared;

namespace Infrastructure.State;

public class GrainStateInfo
{
    public required string TableName { get; init; }
    public required GrainKeyType KeyType { get; init; }
    public required Type Type { get; init; }
    public required string Name { get; init; }
}
