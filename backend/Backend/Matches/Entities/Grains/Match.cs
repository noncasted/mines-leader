using Backend.Users;
using Common;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;
using Shared;

namespace Backend.Matches;

[Reentrant]
public class Match : Grain, IMatch
{
    public Match([States.Match] ITransactionalState<MatchState> state)
    {
        _state = state;
    }
    
    private readonly ITransactionalState<MatchState> _state;

    public Task Setup(GameMatchType type, IReadOnlyList<Guid> participants)
    {
        return _state.PerformUpdate(state =>
        {
            state.Type = type;
            state.Date = DateTime.UtcNow;
            state.Participants = participants;
        });
    }

    public async Task OnComplete(Guid winner)
    {
        var state = await _state.Update(state =>
        {
            state.Winner = winner;
            state.Time = DateTime.UtcNow - state.Date;
        });

        var overview = state.CreateOverview(this.GetPrimaryKey());

        foreach (var participant in state.Participants)
        {
            var userMatchHistory = GrainFactory.GetGrain<IUserMatchHistory>(participant);
            await userMatchHistory.Add(overview);
        }
    }
}