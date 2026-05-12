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
[GrainEventState(State = "user_entity", Lookup = "User", Key = GrainKeyType.Guid)]
public class UserState : IEventStateValue, IProjectionPayload
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;

    public int Version => 0;

    public void Apply(UserInitialized e) => Id = e.Id.ToString();
    public void Apply(UserNameChanged e) => Name = e.Name;

    public INetworkContext ToContext() => new SharedBackendUser.ProfileProjection()
    {
        Id = Guid.Parse(Id),
        Name = Name
    };
}

public class UserInitialized
{
    public Guid Id { get; set; }
}

public class UserNameChanged
{
    public string Name { get; set; } = string.Empty;
}

public class User : UserGrain, IUser
{
    public User(
        [EventState] EventState<UserState> state,
        IUserCollection collection,
        ILogger<User> logger)
    {
        _state = state;
        _collection = collection;
        _logger = logger;
    }

    private readonly EventState<UserState> _state;
    private readonly IUserCollection _collection;
    private readonly ILogger<User> _logger;

    public async Task Initialize()
    {
        await _state.Read();
        
        var userId = this.GetPrimaryKey();
        await _state.Append(new UserInitialized { Id = userId });
        await _state.WriteSession();
        
        var state = _state.Value;
        _logger.LogInformation("[User] Created user {Id} with name {Name}", state.Id, state.Name);

        await this.SendProjection(state);
        await _collection.OnUpdatedTransactional(userId, state);
    }

    public async Task SetName(string name)
    {
        await _state.Read();
        
        await _state.Append(new UserNameChanged { Name = name });
        await _state.WriteSession();
        
        var state = _state.Value;
        var userId = this.GetPrimaryKey();
        _logger.LogInformation("[User] User {Id} changed name to {name}", state.Id, state.Name);

        await this.SendProjection(state);
        await _collection.OnUpdatedTransactional(userId, state);
    }

    public Task<UserState> GetState()
    {
        return _state.ReadAndReturn();
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return Task.FromResult((IProjectionPayload)_state.Value);
    }
}

public static class UserEventStateExtensions
{
    public static async Task<UserState> ReadAndReturn(this EventState<UserState> state)
    {
        await state.Read();
        return state.Value;
    }
}
