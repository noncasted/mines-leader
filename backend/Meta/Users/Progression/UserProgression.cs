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

public interface IUserProgression : IUserGrain
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
[GrainState(Table = "state_user_progression", State = "user_progression", Lookup = "UserProgression",
    Key = GrainKeyType.Guid)]
public class UserProgressionState : IProjectionPayload, IStateValue
{
    [Id(0)] public List<IUserProgressionRecord> Records { get; } = new();

    public int Version => 0;

    public void AddRecord(IUserProgressionRecord record)
    {
        Records.Add(record);
    }

    public int CalculateTotal()
    {
        return Records.Sum(total => total.GetExperience());
    }

    public INetworkContext ToContext() => new SharedBackendUser.ProgressionProjection()
    {
        Experience = CalculateTotal()
    };
}

public class UserProgression : UserGrain, IUserProgression
{
    public UserProgression(
        [State] State<UserProgressionState> state,
        ILogger<UserProgression> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<UserProgressionState> _state;
    private readonly ILogger<UserProgression> _logger;

    public async Task AddRecord(IUserProgressionRecord record)
    {
        _logger.LogInformation("[User] [Progression] User {Id} received experience {Amount} from {RecordType}",
            this.GetPrimaryKey(),
            record.GetExperience(),
            record.GetType().FullName);

        var state = await _state.Update(state => state.AddRecord(record));
        await this.SendCachedProjection(state);

        RegisterLootSideEffect();
    }

    public Task<int> GetTotal()
    {
        return _state.Read(state => state.CalculateTotal());
    }

    public async Task AdjustProgression(int delta)
    {
        var record = new UserProgressionRecords.AdminAdjust { Date = DateTime.UtcNow, Value = delta };
        var state = await _state.Update(s => s.AddRecord(record));
        await this.SendCachedProjection(state);

        RegisterLootSideEffect();
    }

    public async Task SetProgression(int value)
    {
        var state = await _state.Update(s => {
            s.Records.Clear();
            s.AddRecord(new UserProgressionRecords.AdminAdjust { Date = DateTime.UtcNow, Value = value });
        });
        await this.SendCachedProjection(state);

        RegisterLootSideEffect();
    }

    private void RegisterLootSideEffect()
    {
        new LootProgressionSideEffect
        {
            UserId = this.GetPrimaryKey()
        }.AddToTransaction();
    }
}
