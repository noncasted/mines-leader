using Infrastructure;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;
using Shared;

namespace Meta.Users;

public interface IUserProgression : IUserGrain
{
    [Transaction(TransactionOption.Join)]
    Task AddRecord(IUserProgressionRecord record);
}

[Alias(States.User_Progression)]
[GenerateSerializer]
public class UserProgressionState : IProjectionPayload
{
    [Id(0)] public List<IUserProgressionRecord> Records { get; } = new();

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

[Reentrant]
public class UserProgression : UserGrain, IUserProgression
{
    public UserProgression(
        [States.UserProgression] ITransactionalState<UserProgressionState> state,
        ILogger<UserProgression> logger)
    {
        _state = state;
        _logger = logger;
    }
    
    private readonly ITransactionalState<UserProgressionState> _state;
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