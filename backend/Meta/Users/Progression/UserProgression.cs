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
public class ProgressionAdded
{
    [Id(0)]
    public IUserProgressionRecord Record { get; set; } = null!;
}

[GenerateSerializer]
public class ProgressionReset
{
    [Id(0)]
    public int Value { get; set; }
}

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

        await _state.Read();
        await _state.Append(new ProgressionAdded { Record = record });
        await _state.WriteSession();
        await this.SendProjection(_state.Value);

        RegisterLootSideEffect();
    }

    public Task<int> GetTotal()
    {
        // Use a synchronous-looking wrapper for the projection if possible,
        // but in EventState we must call Read().
        // Since the interface is Task<int>, we can just await.
        return _state.ReadAndGetTotal();
    }

    public async Task AdjustProgression(int delta)
    {
        var record = new UserProgressionRecords.AdminAdjust { Date = DateTime.UtcNow, Value = delta };
        await AddRecord(record);
    }

    public async Task SetProgression(int value)
    {
        await _state.Read();
        await _state.Append(new ProgressionReset { Value = value });
        await _state.WriteSession();
        await this.SendProjection(_state.Value);

        RegisterLootSideEffect();
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return Task.FromResult((IProjectionPayload)_state.Value);
    }

    private void RegisterLootSideEffect()
    {
        new LootProgressionSideEffect
        {
            UserId = this.GetPrimaryKey()
        }.AddToTransaction();
    }
}

public static class UserProgressionEventStateExtensions
{
    public static async Task<int> ReadAndGetTotal(this EventState<UserProgressionState> state)
    {
        await state.Read();
        return state.Value.CalculateTotal();
    }
}
