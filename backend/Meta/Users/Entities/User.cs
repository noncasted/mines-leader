using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Shared;

namespace Meta.Users;

public interface IUser : IUserGrain
{
    [Transaction]
    Task Initialize();

    [Transaction]
    Task SetName(string name);
}

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

[Reentrant]
public class User : UserGrain, IUser
{
    public User(
        [State] State<UserState> state,
        ILogger<User> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<UserState> _state;
    private readonly ILogger<User> _logger;

    public async Task Initialize()
    {
        var state = await _state.Update(state =>
            {
                state.Id = this.GetPrimaryKey();
            }
        );

        _logger.LogInformation("[User] Created user {Id} with name {Name}", state.Id, state.Name);

        await this.SendCachedProjection(state);
    }

    public async Task SetName(string name)
    {
        var state = await _state.Update(state =>
            {
                state.Name = name;
            }
        );

        _logger.LogInformation("[User] User {Id} changed name to {name}", state.Id, state.Name);

        await this.SendCachedProjection(state);
    }
}