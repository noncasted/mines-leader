using Common;

namespace Backend.Users;

[Alias(States.User_Projection)]
[GenerateSerializer]
public class UserProjectionState
{
    [Id(0)]
    public Dictionary<string, IProjectionPayload> Values { get; } = new();
}