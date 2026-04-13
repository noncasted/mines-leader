using Common;
using Infrastructure;
using Infrastructure.State;
using Meta.Matches;

namespace Meta.Users;

public interface IUserMatchHistory : IUserGrain
{
    [Transaction]
    Task Add(MatchOverview match);

    [Transaction]
    Task<IReadOnlyList<MatchOverview>> GetBlock(int count);
}

[GenerateSerializer]
[GrainState(Table = "state_user_match_history", State = "user_match_history", Lookup = "UserMatchHistory",
    Key = GrainKeyType.Guid)]
public class UserMatchHistoryState : IStateValue
{
    [Id(0)] public List<MatchOverview> Matches { get; } = new();

    public int Version => 0;
}

public class UserMatchHistory : UserGrain, IUserMatchHistory
{
    public UserMatchHistory([State] State<UserMatchHistoryState> state)
    {
        _state = state;
    }

    private readonly State<UserMatchHistoryState> _state;

    public Task Add(MatchOverview match)
    {
        return Task.WhenAll(_state.Write(state => state.Matches.Add(match)),
            this.SendCachedProjection(match));
    }

    public Task<IReadOnlyList<MatchOverview>> GetBlock(int count)
    {
        return _state.Read(state => (IReadOnlyList<MatchOverview>)state.Matches.TakeLast(count).ToList());
    }
}