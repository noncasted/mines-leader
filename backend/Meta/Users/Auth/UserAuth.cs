using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Users;

[GenerateSerializer]
[GrainEventState(State = "user_auth", Lookup = "UserAuth", Key = GrainKeyType.Guid)]
public class UserAuthState : IEventStateValue
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public bool IsExists { get; set; }
    [Id(2)] public DateTime RegisteredAt { get; set; }

    // S3: SHA-256 of the guest secret (GuestSecrets), base64. Empty - no secret was issued: a user made before the
    // secret (it cannot be entered any more) or a bot.
    [Id(3)] public string GuestSecretHash { get; set; } = string.Empty;

    public int Version => 0;

    public void Apply(UserRegistered e)
    {
        IsExists = true;
        RegisteredAt = e.RegisteredAt;
    }

    public void Apply(GuestSecretIssued e)
    {
        GuestSecretHash = e.Hash;
    }
}

public record UserRegistered(DateTime RegisteredAt);

public record GuestSecretIssued(string Hash, DateTime IssuedAt);

[GenerateSerializer]
public class UserAuthOverview
{
    [Id(0)] public bool IsExists { get; init; }
    [Id(1)] public string GuestSecretHash { get; init; } = string.Empty;

    public bool HasGuestSecret => GuestSecretHash.Length != 0;
}

public interface IUserAuth : IUserGrain
{
    [Transaction]
    Task<bool> IsExists();

    [Transaction]
    Task OnRegistered();

    [Transaction]
    Task<DateTime> GetDate();

    [Transaction]
    Task<UserAuthOverview> GetOverview();

    // Once per user: the guest of a new secret must exist and have no secret yet.
    [Transaction]
    Task IssueGuestSecret(string hash);
}

public class UserAuth : UserGrain, IUserAuth
{
    public UserAuth(
        [EventState] EventState<UserAuthState> state,
        ILogger<UserAuth> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly EventState<UserAuthState> _state;
    private readonly ILogger<UserAuth> _logger;

    public async Task<bool> IsExists()
    {
        var state = await _state.Read();
        return state.IsExists;
    }

    public async Task OnRegistered()
    {
        var state = await _state.Apply(new UserRegistered(DateTime.UtcNow));

        _logger.LogInformation("[User] [Auth] User {UserId} registered", this.GetPrimaryKey());
    }

    public async Task<DateTime> GetDate()
    {
        var state = await _state.Read();
        return state.RegisteredAt;
    }

    public async Task<UserAuthOverview> GetOverview()
    {
        var state = await _state.Read();

        return new UserAuthOverview
        {
            IsExists = state.IsExists,
            GuestSecretHash = state.GuestSecretHash
        };
    }

    public async Task IssueGuestSecret(string hash)
    {
        if (string.IsNullOrEmpty(hash) == true)
            throw new ArgumentException("An empty guest secret hash", nameof(hash));

        var state = await _state.Read();
        var userId = this.GetPrimaryKey();

        if (state.IsExists == false)
            throw new InvalidOperationException($"A guest secret is issued to a user {userId} that does not exist");

        if (state.GuestSecretHash.Length != 0)
            throw new InvalidOperationException($"User {userId} already has a guest secret");

        await _state.Apply(new GuestSecretIssued(hash, DateTime.UtcNow));

        _logger.LogInformation("[User] [Auth] Guest secret issued to {UserId}", userId);
    }
}
