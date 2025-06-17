using Common;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Backend.Users;

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