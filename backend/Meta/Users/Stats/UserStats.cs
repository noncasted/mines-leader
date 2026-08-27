using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUserStats : IUserGrain, IUserProjectionSource
{
    [Transaction]
    Task Apply(UserStatsDelta delta);

    [Transaction]
    Task<UserStatsState> GetState();

    [Transaction]
    Task Reset();
}

[GenerateSerializer]
[GrainEventState(State = "user_stats", Lookup = "UserStats", Key = GrainKeyType.Guid)]
public class UserStatsState : IEventStateValue, IProjectionPayload, IUserStatsState
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public Dictionary<UserStatType, long> Counters { get; set; } = new();
    [Id(2)] public Dictionary<CardGroup, long> CardsPlayedByGroup { get; set; } = new();

    public int Version => 0;

    public void Apply(UserStatsRecorded e)
    {
        foreach (var (type, amount) in e.Delta.Counters)
            Counters[type] = Counters.GetValueOrDefault(type) + amount;

        foreach (var (group, amount) in e.Delta.CardsPlayedByGroup)
            CardsPlayedByGroup[group] = CardsPlayedByGroup.GetValueOrDefault(group) + amount;
    }

    public void Apply(UserStatsReset e)
    {
        Counters.Clear();
        CardsPlayedByGroup.Clear();
    }

    public long Get(UserStatType type) => Counters.GetValueOrDefault(type);

    public long GetCardsPlayed(CardGroup group) => CardsPlayedByGroup.GetValueOrDefault(group);

    public INetworkContext ToContext() => new SharedBackendUser.UserStatsProjection
    {
        Counters = new Dictionary<UserStatType, long>(Counters),
        CardsPlayedByGroup = new Dictionary<CardGroup, long>(CardsPlayedByGroup)
    };
}

[GenerateSerializer]
public record UserStatsRecorded(UserStatsDelta Delta);

[GenerateSerializer]
public record UserStatsReset;

public class UserStats : UserGrain, IUserStats
{
    public UserStats(
        [EventState] EventState<UserStatsState> state,
        ILogger<UserStats> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly EventState<UserStatsState> _state;
    private readonly ILogger<UserStats> _logger;

    public async Task Apply(UserStatsDelta delta)
    {
        if (delta.IsEmpty == true)
            return;

        _logger.LogInformation("[User] [Stats] User {Id} received {Count} stat updates",
            this.GetPrimaryKey(),
            delta.Counters.Count + delta.CardsPlayedByGroup.Count);

        var state = await _state.Apply(new UserStatsRecorded(delta));
        await this.SendProjection(state);

        RegisterAchievementsSideEffect();
    }

    public Task<UserStatsState> GetState()
    {
        return _state.Read();
    }

    public async Task Reset()
    {
        var state = await _state.Apply(new UserStatsReset());
        await this.SendProjection(state);
    }

    private void RegisterAchievementsSideEffect()
    {
        new InGameAchievementsSideEffect
        {
            UserId = this.GetPrimaryKey()
        }.AddToTransaction();
    }

    public async Task<IProjectionPayload> GetProjection()
    {
        var state = await _state.Read();
        return state;
    }
}