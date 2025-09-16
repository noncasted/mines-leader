using Common;
using Infrastructure.Orleans;

namespace Backend.Users;

public class UserHandle
{
    public UserHandle(IOrleans orleans, Guid id)
    {
        _orleans = orleans;
        _id = id;
    }
    
    private readonly IOrleans _orleans;
    private readonly Guid _id;
    
    public IUser Entity => _orleans.GetGrain<IUser>(_id);
    public IUserProgression Progression => _orleans.GetGrain<IUserProgression>(_id);
    public IUserMatchHistory MatchHistory => _orleans.GetGrain<IUserMatchHistory>(_id);
    public IUserDeck Deck => _orleans.GetGrain<IUserDeck>(_id);
}

public static class UserHandleExtensions
{
    public static UserHandle CreateUserHandle(this IOrleans orleans, Guid id)
    {
        return new UserHandle(orleans, id);
    }
}