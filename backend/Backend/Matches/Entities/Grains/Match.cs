using Backend.Users;
using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.Options;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;
using Shared;

namespace Backend.Matches;

[Reentrant]
public class Match : Grain, IMatch
{
    public Match(
        [States.Match] ITransactionalState<MatchState> state,
        IOrleans orleans,
        IOptions<ProgressionOptions> options)
    {
        _state = state;
        _orleans = orleans;
        _options = options;
    }

    private readonly ITransactionalState<MatchState> _state;
    private readonly IOrleans _orleans;
    private readonly IOptions<ProgressionOptions> _options;

    public Task Setup(GameMatchType type, IReadOnlyList<Guid> participants)
    {
        return _state.PerformUpdate(state =>
            {
                state.Type = type;
                state.StartDate = DateTime.UtcNow;
                state.Participants = participants;
            }
        );
    }

    public async Task OnComplete(Guid winnerId)
    {
        var endDate = DateTime.UtcNow;

        var state = await _state.Update(state =>
            {
                state.Winner = winnerId;
                state.Time = endDate - state.StartDate;
            }
        );

        var loserId = state.Participants.First(p => p != winnerId);
        
        var winner = _orleans.CreateUserHandle(winnerId);
        var loser = _orleans.CreateUserHandle(loserId);

        var overview = state.CreateOverview(this.GetPrimaryKey());
        
        var winRecord = new UserProgressionRecords.Win
        {
            Date = endDate,
            Experience = _options.Value.WinExperience
        };

        var lossRecord = new UserProgressionRecords.Loss
        {
            Date = endDate,
            Experience = _options.Value.LossExperience
        };
        
        await Task.WhenAll(
            winner.MatchHistory.Add(overview),
            winner.Progression.AddRecord(winRecord),
            loser.MatchHistory.Add(overview),
            loser.Progression.AddRecord(lossRecord)
        );
    }
}