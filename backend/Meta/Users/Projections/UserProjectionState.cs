namespace Meta.Users;

[GenerateSerializer]
public class UserProjectionState
{
    [Id(0)]
    public Dictionary<string, IProjectionPayload> Values { get; } = new();
    
    [Id(1)]
    public bool IsConnected { get; set; }
}