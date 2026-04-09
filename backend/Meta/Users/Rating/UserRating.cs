using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

public interface IUserRatingRecord
{
    DateTime Date { get; }

    int GetRating();
}

public interface IUserRating : IUserGrain
{
    [Transaction]
    Task AddRecord(IUserRatingRecord record);

    [Transaction]
    Task<int> GetTotal();
}

[GenerateSerializer]
public class UserRatingState : IProjectionPayload, IStateValue
{
    [Id(0)] public List<IUserRatingRecord> Records { get; } = new();

    public int Version => 0;

    public void AddRecord(IUserRatingRecord record)
    {
        Records.Add(record);
    }

    public int CalculateTotal()
    {
        return Records.Sum(r => r.GetRating());
    }

    public INetworkContext ToContext() => new SharedBackendUser.RatingProjection()
    {
        Rating = CalculateTotal()
    };
}

public class UserRating : UserGrain, IUserRating
{
    public UserRating(
        [State] State<UserRatingState> state,
        ILogger<UserRating> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<UserRatingState> _state;
    private readonly ILogger<UserRating> _logger;

    public async Task AddRecord(IUserRatingRecord record)
    {
        _logger.LogInformation("[User] [Rating] User {Id} received rating {Amount} from {RecordType}",
            this.GetPrimaryKey(),
            record.GetRating(),
            record.GetType().FullName);

        var state = await _state.Update(state => state.AddRecord(record));
        await this.SendCachedProjection(state);
    }

    public async Task<int> GetTotal()
    {
        return await _state.Read(s => s.CalculateTotal());
    }
}