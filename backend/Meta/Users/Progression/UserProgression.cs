using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUserProgressionRecord
{
    DateTime Date { get; }

    int GetExperience();
}

public interface IUserProgression : IUserGrain, IUserProjectionSource
{
    [Transaction]
    Task AddRecord(IUserProgressionRecord record);

    [Transaction]
    Task<int> GetTotal();

    [Transaction]
    Task AdjustProgression(int delta);

    [Transaction]
    Task SetProgression(int value);
}

[GenerateSerializer]
[GrainEventState(State = "user_progression", Lookup = "UserProgression",
    Key = GrainKeyType.Guid)]
public class UserProgressionState : IEventStateValue, IProjectionPayload
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public List<IUserProgressionRecord> Records { get; set; } = new();

    public int Version => 0;

    public void Apply(ProgressionAdded e) => Records.Add(e.Record);
    public void Apply(ProgressionReset e)
    {
        Records.Clear();
        Records.Add(new UserProgressionRecords.AdminAdjust { Date = DateTime.UtcNow, Value = e.Value });
    }

    public int CalculateTotal() => Records.Sum(r => r.GetExperience());

    public INetworkContext ToContext() => new SharedBackendUser.ProgressionProjection()
    {
        Experience = CalculateTotal()
    };
}

[GenerateSerializer]
public record ProgressionAdded(IUserProgressionRecord Record);

[GenerateSerializer]
public record ProgressionReset(int Value);

public class UserProgression : UserGrain, IUserProgression
{
    public UserProgression(
        [EventState] EventState<UserProgressionState> state,
        ILogger<UserProgression> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly EventState<UserProgressionState> _state;
    private readonly ILogger<UserProgression> _logger;

    public async Task AddRecord(IUserProgressionRecord record)
    {
        _logger.LogInformation("[User] [Progression] User {Id} received experience {Amount} from {RecordType}",
            this.GetPrimaryKey(),
            record.GetExperience(),
            record.GetType().FullName);

        var state = await _state.Apply(new ProgressionAdded(record));
        await this.SendProjection(state);

        RegisterLootSideEffect();
    }

    public async Task<int> GetTotal()
    {
        var state = await _state.Read();
        return state.CalculateTotal();
    }

    public async Task AdjustProgression(int delta)
    {
        var record = new UserProgressionRecords.AdminAdjust { Date = DateTime.UtcNow, Value = delta };
        await AddRecord(record);
    }

    public async Task SetProgression(int value)
    {
        var state = await _state.Apply(new ProgressionReset(value));
        await this.SendProjection(state);

        RegisterLootSideEffect();
    }

    public async Task<IProjectionPayload> GetProjection()
    {
        var state = await _state.Read();
        return state;
    }

    private void RegisterLootSideEffect()
    {
        new LootProgressionSideEffect
        {
            UserId = this.GetPrimaryKey()
        }.AddToTransaction();
    }
}

