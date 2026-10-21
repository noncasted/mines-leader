using Meta.Users;
using Shared;

namespace MetaGateway.UserFlow.Connection;

public class MetaSocketAuthResult
{
    public required SharedBackendSocketAuth.Response Response { get; init; }

    // The user of an accepted connection, null for a refusal.
    public ResolvedUser? User { get; init; }
}

/// <summary>
/// S3: the first frame of the meta socket -> the user of the connection (see <see cref="SharedBackendSocketAuth.Request"/>).
/// A refusal is a status, an exception is a failure of the cluster.
/// </summary>
public class MetaSocketAuth
{
    public MetaSocketAuth(IUserFactory userFactory, ILogger<MetaSocketAuth> logger)
    {
        _userFactory = userFactory;
        _logger = logger;
    }

    private readonly IUserFactory _userFactory;
    private readonly ILogger<MetaSocketAuth> _logger;

    public async Task<MetaSocketAuthResult> Authenticate(SharedBackendSocketAuth.Request request)
    {
        var hasId = request.GuestId != Guid.Empty;
        var hasSecret = string.IsNullOrEmpty(request.GuestSecret) == false;

        if (hasId == false && hasSecret == false)
            return Accept(await _userFactory.CreateGuest());

        if (hasId == false || hasSecret == false)
            return Refuse(SharedBackendSocketAuth.AuthStatus.InvalidRequest, "a guest without its id or secret");

        var user = await _userFactory.ResolveGuest(request.GuestId, request.GuestSecret);

        if (user == null)
            return Refuse(SharedBackendSocketAuth.AuthStatus.InvalidCredentials, $"guest {request.GuestId}");

        return Accept(user);
    }

    private static MetaSocketAuthResult Accept(ResolvedUser user)
    {
        return new MetaSocketAuthResult
        {
            User = user,
            Response = new SharedBackendSocketAuth.Response
            {
                Status = SharedBackendSocketAuth.AuthStatus.Ok,
                UserId = user.Id,
                IssuedSecret = user.IssuedSecret
            }
        };
    }

    private MetaSocketAuthResult Refuse(SharedBackendSocketAuth.AuthStatus status, string reason)
    {
        _logger.LogWarning("[Meta] [Auth] Refused: {Status}, {Reason}", status, reason);

        return new MetaSocketAuthResult
        {
            Response = new SharedBackendSocketAuth.Response
            {
                Status = status
            }
        };
    }
}
