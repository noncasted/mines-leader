using Infrastructure;
using Meta.Matches;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Meta.Users;

public interface IUserMatchHistory : IUserGrain
{
    [Transaction(TransactionOption.Join)]
    Task Add(MatchOverview match);
}

[Alias(States.User_MatchHistory)]
[GenerateSerializer]
public class UserMatchHistoryState
{
    [Id(0)] public List<MatchOverview> Matches { get; } = new();
}

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