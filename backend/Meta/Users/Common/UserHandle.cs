using Infrastructure;

namespace Meta.Users;

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
    public IUserStats Stats => _orleans.GetGrain<IUserStats>(_id);
    public IUserInGameAchievements Achievements => _orleans.GetGrain<IUserInGameAchievements>(_id);
    public IUserRating Rating => _orleans.GetGrain<IUserRating>(_id);
    public IUserMatchHistory MatchHistory => _orleans.GetGrain<IUserMatchHistory>(_id);
    public IUserDeck Deck => _orleans.GetGrain<IUserDeck>(_id);
    public IUserCards Cards => _orleans.GetGrain<IUserCards>(_id);
    public IUserAuth Auth => _orleans.GetGrain<IUserAuth>(_id);
}

public static class UserHandleExtensions
{
    public static UserHandle CreateUserHandle(this IOrleans orleans, Guid id)
    {
        return new UserHandle(orleans, id);
    }
}