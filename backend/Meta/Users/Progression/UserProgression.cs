using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUserProgression : IUserGrain
{
    [Transaction]
    Task AddRecord(IUserProgressionRecord record);
}

[GenerateSerializer]
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
    }
}