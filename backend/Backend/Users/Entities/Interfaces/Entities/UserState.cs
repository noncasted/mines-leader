using Common;
using Shared;

namespace Backend.Users;

[Alias(States.User_Entity)]
[GenerateSerializer]
public class UserState : IProjectionPayload
{
    [Id(0)] public Guid Id { get; set; }

    [Id(1)] public string Name { get; set; } = string.Empty;

    public INetworkContext ToContext() => new SharedBackendUser.ProfileProjection()
    {
        Id = Id,
        Name = Name
    };
}