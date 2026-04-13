using Common;
using Infrastructure.State;

namespace Meta.Users;

[GenerateSerializer]
[GrainState(Table = "state_user_projection", State = "user_projection", Lookup = "UserProjection",
    Key = GrainKeyType.Guid)]
public class UserProjectionState : IStateValue
{
    [Id(0)]
    public Dictionary<string, IProjectionPayload> Values { get; } = new();

    [Id(1)]
    public bool IsConnected { get; set; }

    public int Version => 0;
}