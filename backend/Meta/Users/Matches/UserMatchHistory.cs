using Common;
using Infrastructure;
using Infrastructure.State;
using Meta.Matches;
using Shared;

namespace Meta.Users;

public interface IUserMatchHistory : IUserGrain
{
    [Transaction]
    Task Add(MatchOverview match);

    [Transaction]
    Task<IReadOnlyList<MatchOverview>> GetBlock(int count);
}

[GenerateSerializer]
[GrainEventState(State = "user_match_history", Lookup = "UserMatchHistory",
    Key = GrainKeyType.Guid)]
public class UserMatchHistoryAggregate : IEventStateValue
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public List<MatchOverview> Matches { get; set; } = new();

    public int Version => 0;

    public void Apply(MatchAdded e) => Matches.Add(e.Match);
}

public record MatchAdded(MatchOverview Match);

public class UserMatchHistory : UserGrain, IUserMatchHistory
{
    public UserMatchHistory(
        [EventState] EventState<UserMatchHistoryAggregate> state)
    {
        _state = state;
    }

    private readonly EventState<UserMatchHistoryAggregate> _state;

    public async Task Add(MatchOverview match)
    {
        await _state.Apply(new MatchAdded(match));
        await this.SendProjection(match);
    }

    public async Task<IReadOnlyList<MatchOverview>> GetBlock(int count)
    {
        var state = await _state.Read();
        return state.Matches.TakeLast(count).ToList();
    }
}
