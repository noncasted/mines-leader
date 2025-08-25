using Backend.Users;
using Common;
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

    public async Task OnComplete(Guid winner)
    {
        var endDate = DateTime.UtcNow;

        var state = await _state.Update(state =>
            {
                state.Winner = winner;
                state.Time = endDate - state.StartDate;
            }
        );

        var overview = state.CreateOverview(this.GetPrimaryKey());

        await _orleans
            .GetGrains<IUserMatchHistory>(state.Participants)
            .Iterate(history => history.Add(overview));

        var loser = state.Participants.First(p => p != winner);
        var winnerProgression = _orleans.GetGrain<IUserProgression>(winner);
        var loserProgression = _orleans.GetGrain<IUserProgression>(loser);

        await Task.WhenAll(
            winnerProgression.AddRecord(new UserProgressionRecords.Win
                {
                    Date = endDate,
                    Experience = _options.Value.WinExperience
                }
            ),
            loserProgression.AddRecord(new UserProgressionRecords.Loss
                {
                    Date = endDate,
                    Experience = _options.Value.LossExperience
                }
            )
        );
    }
}