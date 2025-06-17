using Common;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Backend.Users;

[Reentrant]
public class User : UserGrain, IUser
{
    public User(
        [States.UserEntity]
        ITransactionalState<UserState> state,
        ILogger<User> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly ITransactionalState<UserState> _state;
    private readonly ILogger<User> _logger;

    public async Task Initialize(UserCreateOptions options)
    {
        var state = await _state.Update(state =>
        {
            state.Id = this.GetPrimaryKey();
            state.Name = options.Name;
        });
        
        _logger.LogInformation("[User] Created user {Id} with name {Name}", state.Id, state.Name);

        await this.SendCachedProjection(state);
    }

    public async Task SetName(string name)
    {
        var state = await _state.Update(state =>
        {
            state.Name = name;
        });
        
        _logger.LogInformation("[User] User {Id} changed name to {name}", state.Id, state.Name);

        await this.SendCachedProjection(state);
    }
}