using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

[GenerateSerializer]
[GrainEventState(State = "user_auth", Lookup = "UserAuth", Key = GrainKeyType.Guid)]
public class UserAuthState : IEventStateValue
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public bool IsExists { get; set; }
    [Id(2)] public DateTime RegisteredAt { get; set; }

    public int Version => 0;

    public void Apply(UserRegistered e)
    {
        IsExists = true;
        RegisteredAt = e.RegisteredAt;
    }
}

public record UserRegistered(DateTime RegisteredAt);

public interface IUserAuth : IUserGrain
{
    [Transaction]
    Task<bool> IsExists();

    [Transaction]
    Task OnRegistered();

    [Transaction]
    Task<DateTime> GetDate();
}

public class UserAuth : UserGrain, IUserAuth
{
    public UserAuth(
        [EventState] EventState<UserAuthState> state,
        ILogger<UserAuth> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly EventState<UserAuthState> _state;
    private readonly ILogger<UserAuth> _logger;

    public async Task<bool> IsExists()
    {
        var state = await _state.Read();
        return state.IsExists;
    }

    public async Task OnRegistered()
    {
        var state = await _state.Apply(new UserRegistered(DateTime.UtcNow));

        _logger.LogInformation("[User] [Auth] User {UserId} registered", this.GetPrimaryKey());
    }

    public async Task<DateTime> GetDate()
    {
        var state = await _state.Read();
        return state.RegisteredAt;
    }
}
