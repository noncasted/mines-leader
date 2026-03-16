using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Meta.Users;

[GenerateSerializer]
public class UserAuthState
{
    [Id(0)]
    public bool IsExists { get; set; }
}

public interface IUserAuth : IUserGrain
{
    [Transaction]
    Task<bool> IsExists();

    [Transaction]
    Task OnRegistered();
}

[Reentrant]
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
        await _state.Update(state =>
            {
                state.IsExists = true;
            }
        );

        _logger.LogInformation("[User] [Auth] User {UserId} registered", this.GetPrimaryKey());
    }
}