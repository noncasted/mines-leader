using Backend.Matches;
using Common;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Backend.Users;

[Reentrant]
public class UserMatchHistory : UserGrain, IUserMatchHistory
{
    public UserMatchHistory([States.UserMatchHistory] ITransactionalState<UserMatchHistoryState> state)
    {
        _state = state;
    }

    private readonly ITransactionalState<UserMatchHistoryState> _state;

    public Task Add(MatchOverview match)
    {
        return Task.WhenAll(
            _state.PerformUpdate(state => state.Matches.Add(match)),
            this.SendCachedProjection(match)
        );
    }
}