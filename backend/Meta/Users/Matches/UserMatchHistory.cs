using Infrastructure;
using Infrastructure.State;
using Meta.Matches;
using Orleans.Concurrency;

namespace Meta.Users;

public interface IUserMatchHistory : IUserGrain
{
    [Transaction]
    Task Add(MatchOverview match);
}

[GenerateSerializer]
public class UserMatchHistoryState
{
    [Id(0)] public List<MatchOverview> Matches { get; } = new();
}

[Reentrant]
public class UserMatchHistory : UserGrain, IUserMatchHistory
{
    public UserMatchHistory([State] State<UserMatchHistoryState> state)
    {
        _state = state;
    }

    private readonly State<UserMatchHistoryState> _state;

    public Task Add(MatchOverview match)
    {
        return Task.WhenAll(
            _state.Write(state => state.Matches.Add(match)),
            this.SendCachedProjection(match)
        );
    }
}