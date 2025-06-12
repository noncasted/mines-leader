namespace Backend.Users.Projections;

[GenerateSerializer]
public class UserProjectionState
{
    [Id(0)]
    public Dictionary<string, IProjectionPayload> Values { get; } = new();
}