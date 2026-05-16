using Common;
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

public interface IUserRating : IUserGrain, IUserProjectionSource
{
    [Transaction]
    Task AddRecord(IUserRatingRecord record);

    [Transaction]
    Task<int> GetTotal();

    [Transaction]
    Task AdjustRating(int delta);

    [Transaction]
    Task SetRating(int value);
}

[GenerateSerializer]
[GrainEventState(State = "user_rating", Lookup = "UserRating", Key = GrainKeyType.Guid)]
public class UserRatingState : IEventStateValue, IProjectionPayload
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public List<IUserRatingRecord> Records { get; set; } = new();

    public int Version => 0;

    public void Apply(RatingAdded e) => Records.Add(e.Record);
    public void Apply(RatingReset e)
    {
        Records.Clear();
        Records.Add(new UserRatingRecords.AdminAdjust { Date = DateTime.UtcNow, Value = e.Value });
    }

    public int CalculateTotal() => Records.Sum(r => r.GetRating());

    public INetworkContext ToContext() => new SharedBackendUser.RatingProjection()
    {
        Rating = CalculateTotal()
    };
}

public record RatingAdded(IUserRatingRecord Record);

public record RatingReset(int Value);

public class UserRating : UserGrain, IUserRating
{
    public UserRating(
        [EventState] EventState<UserRatingState> state,
        ILogger<UserRating> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly EventState<UserRatingState> _state;
    private readonly ILogger<UserRating> _logger;

    public async Task AddRecord(IUserRatingRecord record)
    {
        _logger.LogInformation("[User] [Rating] User {Id} received rating {Amount} from {RecordType}",
            this.GetPrimaryKey(),
            record.GetRating(),
            record.GetType().FullName);

        var state = await _state.Apply(new RatingAdded(record));
        await this.SendProjection(state);
    }

    public async Task<int> GetTotal()
    {
        var state = await _state.Read();
        return state.CalculateTotal();
    }

    public async Task AdjustRating(int delta)
    {
        var record = new UserRatingRecords.AdminAdjust { Date = DateTime.UtcNow, Value = delta };
        await AddRecord(record);
    }

    public async Task SetRating(int value)
    {
        var state = await _state.Apply(new RatingReset(value));
        await this.SendProjection(state);
    }

    public Task<IProjectionPayload> GetProjection()
    {
        return Task.FromResult((IProjectionPayload)_state.Value);
    }
}
