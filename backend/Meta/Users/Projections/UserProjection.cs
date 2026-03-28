using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Meta.Users;

public interface IUserProjection : IGrainWithGuidKey
{
    [Transaction]
    Task OnConnected();

    [Transaction]
    Task OnDisconnected();

    [Transaction]
    Task ForceNotify();

    [Transaction]
    Task SendCached(IProjectionPayload payload);

    [Transaction]
    Task Cache(IProjectionPayload payload);

    [Transaction]
    Task SendOneTime(IProjectionPayload payload);
}

public class UserProjectionChannelId : IRuntimeChannelId
{
    public UserProjectionChannelId(Guid id)
    {
        _id = id;
    }

    private readonly Guid _id;

    public string ToRaw()
    {
        return $"user-projection-{_id}";
    }
}

public class UserProjection : Grain, IUserProjection
{
    public UserProjection(
        [State] State<UserProjectionState> state,
        IMessaging messaging,
        ILogger<UserProjection> logger)
    {
        _state = state;
        _messaging = messaging;
        _logger = logger;
        _channelId = new UserProjectionChannelId(this.GetPrimaryKey());
    }

    private readonly State<UserProjectionState> _state;
    private readonly IMessaging _messaging;
    private readonly ILogger<UserProjection> _logger;
    private readonly UserProjectionChannelId _channelId;

    public Task OnConnected()
    {
        return _state.Write(state => state.IsConnected = true);
    }

    public Task OnDisconnected()
    {
        return _state.Write(state => state.IsConnected = false);
    }

    public async Task ForceNotify()
    {
        var state = await _state.ReadValue();

        if (state.IsConnected == false)
        {
            _logger.LogTrace("[User] [Projection] Failed to force notify. User {Id} is not connected",
                this.GetPrimaryKey()
            );
            return;
        }

        foreach (var (_, value) in state.Values)
            await Send(value);
    }

    public async Task SendCached(IProjectionPayload payload)
    {
        _logger.LogInformation("[User] [Projection] Sending cached {Type} to {Id}",
            payload.GetType().Name,
            this.GetPrimaryKey()
        );

        var state = await _state.Update(state => state.Values[payload.GetType().Name] = payload);

        if (state.IsConnected == false)
        {
            _logger.LogTrace("[User] [Projection] Failed to send cached. User {Id} is not connected",
                this.GetPrimaryKey()
            );
            
            return;
        }

        await Send(payload);
    }

    public Task Cache(IProjectionPayload payload)
    {
        _logger.LogInformation("[User] [Projection] Saving cached {Type} to {Id}",
            payload.GetType().Name,
            this.GetPrimaryKey()
        );

        return _state.Write(state => state.Values[payload.GetType().Name] = payload);
    }

    public async Task SendOneTime(IProjectionPayload payload)
    {
        _logger.LogInformation("[User] [Projection] Sending one time {Type} to {Id}",
            payload.GetType().Name,
            this.GetPrimaryKey()
        );

        var state = await _state.ReadValue();

        if (state.IsConnected == false)
        {
            _logger.LogTrace("[User] [Projection] Failed to send one time. User {Id} is not connected",
                this.GetPrimaryKey()
            );

            return;
        }

        await Send(payload);
    }

    private Task Send(IProjectionPayload payload)
    {
        return _messaging.PublishChannel(_channelId, payload);
    }
}