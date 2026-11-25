using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meta.Users;

/// <summary>
/// Who the user of a meta socket is (S3). A user is entered only with the secret it was issued: a bare id is never
/// enough. Every method is one transaction. The projections are not read here: the connection reads its snapshot after
/// it subscribes to the updates (UserConnectionEntryPoint), a read here would be thrown away. Null is a refusal of the
/// credentials.
/// </summary>
public interface IUserFactory
{
    // A new guest with a new secret (ResolvedUser.IssuedSecret).
    Task<ResolvedUser> CreateGuest();

    // A guest saved by the client. An id unknown to the server (a wiped database) gets a new guest; a known one is
    // entered only with its secret. A user without a secret (made before the secret existed) is refused: there is no
    // migration window, such a client gets nothing by its id alone.
    Task<ResolvedUser?> ResolveGuest(Guid guestId, string secret);
}

[GenerateSerializer]
public class UserCreateOptions
{
    [Id(0)] public string? Name { get; set; }
}

public class ResolvedUser
{
    public required Guid Id { get; init; }

    // Set only when a guest secret was issued by this call: the client saves it, it is never sent again.
    public string IssuedSecret { get; init; } = string.Empty;
}

public class UserFactory : IUserFactory
{
    public UserFactory(IOrleans orleans, ILogger<UserFactory> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ILogger<UserFactory> _logger;

    public Task<ResolvedUser> CreateGuest()
    {
        return _orleans.Transactions.Run(CreateGuestInternal);
    }

    public Task<ResolvedUser?> ResolveGuest(Guid guestId, string secret)
    {
        if (guestId == Guid.Empty)
            throw new ArgumentException("An empty guest id", nameof(guestId));

        return _orleans.Transactions.Run<ResolvedUser?>(async () => {
            var overview = await _orleans.CreateUserHandle(guestId).Auth.GetOverview();

            if (overview.IsExists == false)
            {
                _logger.LogWarning("[User] [Auth] Unknown guest {UserId}: a new guest is created", guestId);
                return await CreateGuestInternal();
            }

            if (overview.HasGuestSecret == false)
            {
                _logger.LogWarning("[User] [Auth] Guest login refused: {UserId} has no secret", guestId);
                return null;
            }

            if (GuestSecrets.TryHash(secret, out var hash) == false ||
                GuestSecrets.Matches(overview.GuestSecretHash, hash) == false)
            {
                _logger.LogWarning("[User] [Auth] Guest login refused: wrong secret of {UserId}", guestId);
                return null;
            }

            return ToResolved(guestId, string.Empty);
        });
    }

    private async Task<ResolvedUser> CreateGuestInternal()
    {
        var id = await CreateInternal(new UserCreateOptions());
        var (secret, hash) = GuestSecrets.Generate();

        await _orleans.CreateUserHandle(id).Auth.IssueGuestSecret(hash);

        return ToResolved(id, secret);
    }

    private static ResolvedUser ToResolved(Guid id, string issuedSecret)
    {
        return new ResolvedUser
        {
            Id = id,
            IssuedSecret = issuedSecret
        };
    }

    private async Task<Guid> CreateInternal(UserCreateOptions options)
    {
        var id = Guid.NewGuid();

        var handle = _orleans.CreateUserHandle(id);
        var name = options.Name ?? $"User_{id.ToString()[..8]}";

        // The grains are independent, so initialization fans out; only the chain of the entity itself
        // is sequential, where the name is set on top of Initialize.
        await Task.WhenAll(
            InitializeEntity(),
            handle.Deck.Initialize(),
            handle.Cards.Initialize(),
            handle.Auth.OnRegistered());

        return id;

        async Task InitializeEntity()
        {
            await handle.Entity.Initialize();
            await handle.Entity.SetName(name);
        }
    }
}
