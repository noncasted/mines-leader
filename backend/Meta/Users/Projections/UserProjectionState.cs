using Infrastructure;

namespace Meta.Users;

[Alias(States.User_Projection)]
[GenerateSerializer]
public class UserProjectionState
{
    [Id(0)]
    public Dictionary<string, IProjectionPayload> Values { get; } = new();
    
    [Id(1)]
    public bool IsConnected { get; set; }
}