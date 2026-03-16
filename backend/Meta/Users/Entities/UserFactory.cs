using Infrastructure;

namespace Meta.Users;

public interface IUserFactory
{
    Task<Guid> Create(UserCreateOptions options);
}

[GenerateSerializer]
public class UserCreateOptions
{
}

public class UserFactory : IUserFactory
{
    private readonly IOrleans _orleans;

    public UserFactory(IOrleans orleans)
    {
        _orleans = orleans;
    }

    public Task<Guid> Create(UserCreateOptions options)
    {
        return _orleans.Transactions.Run(async () =>
            {
                var id = Guid.NewGuid();

                var handle = _orleans.CreateUserHandle(id);

                await handle.Entity.Initialize();
                await handle.Deck.Initialize();
                await handle.Auth.OnRegistered();
                
                return id;
            }
        );
    }
}