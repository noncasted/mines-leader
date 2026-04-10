using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Meta.Users;

[GenerateSerializer]
[GrainState(Table = "state_user_auth", State = "user_auth", Lookup = "UserAuth", Key = GrainKeyType.Guid)]
public class UserAuthState : IStateValue
{
    [Id(0)] public bool IsExists { get; set; }
    [Id(1)] public DateTime RegisteredAt { get; set; }

    public int Version => 0;
}

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
        [State] State<UserAuthState> state,
        ILogger<UserAuth> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<UserAuthState> _state;
    private readonly ILogger<UserAuth> _logger;

    public Task<bool> IsExists() => _state.Read(state => state.IsExists);

    public async Task OnRegistered()
    {
        await _state.Update(state => {
            state.IsExists = true;
            state.RegisteredAt = DateTime.UtcNow;
        });

        _logger.LogInformation("[User] [Auth] User {UserId} registered", this.GetPrimaryKey());
    }

    public Task<DateTime> GetDate()
    {
        return _state.Read(state => state.RegisteredAt);
    }
}