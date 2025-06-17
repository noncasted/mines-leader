using Common;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans.Transactions.Abstractions;

namespace Backend.Users.Projections;

public class UserProjection : Grain, IUserProjection
{
    public UserProjection(
        [States.UserProjection] ITransactionalState<UserProjectionState> state,
        [States.UserProjectionConnection] IPersistentState<UserProjectionConnectionState> connectionState,
        IMessagingClient messaging,
        ILogger<UserProjection> logger)
    {
        _state = state;
        _connectionState = connectionState;
        _messaging = messaging;
        _logger = logger;
        _userId = this.GetPrimaryKey();
    }

    private readonly ITransactionalState<UserProjectionState> _state;
    private readonly IPersistentState<UserProjectionConnectionState> _connectionState;
    private readonly IMessagingClient _messaging;
    private readonly Guid _userId;
    private readonly ILogger<UserProjection> _logger;

    private readonly List<IProjectionPayload> _pending = new();

    private UserProjectionConnectionState connectionState => _connectionState.State;

    public Task OnConnected(Guid connectionServiceId)
    {
        connectionState.ConnectionServiceId = connectionServiceId;
        return _connectionState.WriteStateAsync();
    }

    public Task OnDisconnected()
    {
        connectionState.ConnectionServiceId = Guid.Empty;
        return _connectionState.WriteStateAsync();
    }

    public async Task ForceNotify()
    {
        if (connectionState.ConnectionServiceId == Guid.Empty)
        {
            _logger.LogWarning("[User] [Projection] No connection service id found for {Id}", this.GetPrimaryKey());
            return;
        }

        var state = await _state.Read();

        foreach (var (_, value) in state.Values)
            await Send(value);

        if (_pending.Count > 0)
        {
            foreach (var payload in _pending)
                await Send(payload);

            _pending.Clear();
        }
    }

    public async Task SendCached(IProjectionPayload payload)
    {
        _logger.LogInformation("[User] [Projection] Sending cached {Type} to {Id}",
            payload.GetType().Name,
            this.GetPrimaryKey());

        await _state.Write(state => state.Values[payload.GetType().Name] = payload);

        if (connectionState.ConnectionServiceId == Guid.Empty)
        {
            _logger.LogWarning("[User] [Projection] No connection service id found for {Id}", this.GetPrimaryKey());
            _pending.Add(payload);
            return;
        }

        await Send(payload);
    }

    public Task Cache(IProjectionPayload payload)
    {
        _logger.LogInformation("[User] [Projection] Saving cached {Type} to {Id}",
            payload.GetType().Name,
            this.GetPrimaryKey());
        
        return _state.Write(state => state.Values[payload.GetType().Name] = payload);
    }

    public Task SendOneTime(IProjectionPayload payload)
    {
        _logger.LogInformation("[User] [Projection] Sending one time {Type} to {Id}",
            payload.GetType().Name,
            this.GetPrimaryKey());

        if (connectionState.ConnectionServiceId == Guid.Empty)
        {
            _logger.LogWarning("[User] [Projection] No connection service id found for {Id}", this.GetPrimaryKey());
            _pending.Add(payload);
            return Task.CompletedTask;
        }

        return Send(payload);
    }

    private Task Send(IProjectionPayload payload)
    {
        return _messaging.Send(connectionState.ConnectionServiceId, new ProjectionPayloadValue
        {
            Value = payload,
            UserId = _userId
        });
    }
}