using Infrastructure;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;
using Shared;

namespace Meta.Users;

public interface IUser : IUserGrain
{
    [Transaction(TransactionOption.Join)]
    Task Initialize();

    [Transaction(TransactionOption.Join)]
    Task SetName(string name);
}

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

[Reentrant]
public class User : UserGrain, IUser
{
    public User(
        [States.UserEntity] ITransactionalState<UserState> state,
        IGrainFactory grains,
        ILogger<User> logger)
    {
        _state = state;
        _grains = grains;
        _logger = logger;
    }

    private readonly ITransactionalState<UserState> _state;
    private readonly IGrainFactory _grains;
    private readonly ILogger<User> _logger;

    public async Task Initialize()
    {
        var state = await _state.Update(state =>
            {
                state.Id = this.GetPrimaryKey();
            }
        );

        _logger.LogInformation("[User] Created user {Id} with name {Name}", state.Id, state.Name);

        var collection = _grains.GetGrain<IUsersCollection>(Guid.Empty);
        await collection.AddOrUpdate(state);

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

        var collection = _grains.GetGrain<IUsersCollection>(Guid.Empty);
        await collection.AddOrUpdate(state);
        
        await this.SendCachedProjection(state);
    }
}