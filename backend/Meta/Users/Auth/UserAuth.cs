using Infrastructure;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Meta.Users;

[Alias(States.User_Auth)]
[GenerateSerializer]
public class UserAuthState
{
    [Id(0)]
    public bool IsExists { get; set; }
}

public interface IUserAuth : IUserGrain
{
    [Transaction(TransactionOption.CreateOrJoin)]
    Task<bool> IsExists();

    [Transaction(TransactionOption.Join)]
    Task OnRegistered();
}

[Reentrant]
public class UserAuth : UserGrain, IUserAuth
{
    public UserAuth(
        [States.UserAuth] ITransactionalState<UserAuthState> state,
        ILogger<UserAuth> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly ITransactionalState<UserAuthState> _state;
    private readonly ILogger<UserAuth> _logger;

    public Task<bool> IsExists() => _state.PerformRead(state => state.IsExists);

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