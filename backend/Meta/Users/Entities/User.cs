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
    [Id(1)] public Guid UserId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;

    public int Version => 0;

    public void Apply(UserInitialized e) => UserId = e.Id;
    public void Apply(UserNameChanged e) => Name = e.Name;

    public INetworkContext ToContext() => new SharedBackendUser.ProfileProjection()
    {
        Id = UserId,
        Name = Name
    };
}

public record UserInitialized(Guid Id);

public record UserNameChanged(string Name);

public partial class User : UserGrain, IUser
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
        var userId = this.GetPrimaryKey();
        var state = await _state.Apply(new UserInitialized(userId));
        LogUserCreatedUserIdWithNameName(state.Id, state.Name);
        await Update(state);
    }

    public async Task SetName(string name)
    {
        var state = await _state.Apply(new UserNameChanged(name));
        LogUserUserIdChangedNameToName(state.Id, state.Name);
        await Update(state);
    }

    public Task<UserState> GetState()
    {
        return _state.Read();
    }

    public async Task<IProjectionPayload> GetProjection()
    {
        var state = await _state.Read();
        return state;
    }

    private Task Update(UserState state)
    {
        return Task.WhenAll(this.SendProjection(state), _collection.OnUpdatedTransactional(state.UserId, state));
    }

    [LoggerMessage(LogLevel.Information, "[User] Created user {Id} with name {Name}")]
    partial void LogUserCreatedUserIdWithNameName(string id, string name);

    [LoggerMessage(LogLevel.Information, "[User] User {Id} changed name to {name}")]
    partial void LogUserUserIdChangedNameToName(string id, string name);
}