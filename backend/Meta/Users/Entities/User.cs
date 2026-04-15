using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUser : IUserGrain, IUserProjectionSource
{
    [Transaction]
    Task Initialize();

    [Transaction]
    Task SetName(string name);

    [Transaction]
    Task<UserState> GetState();
}

[GenerateSerializer]
[GrainState(Table = "state_user_entity", State = "user_entity", Lookup = "User", Key = GrainKeyType.Guid)]
public class UserState : IProjectionPayload, IStateValue
{
    [Id(0)] public Guid Id { get; set; }

    [Id(1)] public string Name { get; set; } = string.Empty;

    public int Version => 0;

    public INetworkContext ToContext() => new SharedBackendUser.ProfileProjection()
    {
        Id = Id,
        Name = Name
    };
}

public class User : UserGrain, IUser
{
    public User(
        [State] State<UserState> state,
        IUserCollection collection,
        ILogger<User> logger)
    {
        _state = state;
        _collection = collection;
        _logger = logger;
    }

    private readonly State<UserState> _state;
    private readonly IUserCollection _collection;
    private readonly ILogger<User> _logger;

    public async Task Initialize()
    {
        var state = await _state.Update(state => {
            state.Id = this.GetPrimaryKey();
        });

        _logger.LogInformation("[User] Created user {Id} with name {Name}", state.Id, state.Name);

        await this.SendProjection(state);
        await _collection.OnUpdatedTransactional(state.Id, state);
    }

    public async Task SetName(string name)
    {
        var state = await _state.Update(state => {
            state.Name = name;
        });

        _logger.LogInformation("[User] User {Id} changed name to {name}", state.Id, state.Name);

        await this.SendProjection(state);
        await _collection.OnUpdatedTransactional(state.Id, state);
    }

    public Task<UserState> GetState()
    {
        return _state.ReadValue();
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return _state.Read(s => (IProjectionPayload)s);
    }
}