using Infrastructure.Orleans;

namespace Backend.Users;

public class UserFactory : IUserFactory
{
    private readonly IOrleans _orleans;

    public UserFactory(IOrleans orleans)
    {
        _orleans = orleans;
    }
 
    public Task<Guid> Create(UserCreateOptions options)
    {
        return _orleans.Transactions.Create(async () =>
        {
            var id = Guid.NewGuid();

            var user = _orleans.Grains.GetGrain<IUser>(id);
            var deck = _orleans.Grains.GetGrain<IUserDeck>(id);
            
            await user.Initialize(options);
            await deck.Initialize();
            
            return id;
        });
    }
}