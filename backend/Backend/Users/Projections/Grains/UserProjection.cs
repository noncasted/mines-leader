using Common;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Orleans.Transactions.Abstractions;

namespace Backend.Users;

public class UserProjection : Grain, IUserProjection
{
    public UserProjection(
        [States.UserProjection] ITransactionalState<UserProjectionState> state,
        IMessaging messaging,
        ILogger<UserProjection> logger)
    {
        _state = state;
        _messaging = messaging;
        _logger = logger;
        _pipeId = new UserProjectionPipeId(this.GetPrimaryKey());
    }

    private readonly ITransactionalState<UserProjectionState> _state;
    private readonly IMessaging _messaging;
    private readonly ILogger<UserProjection> _logger;
    private readonly UserProjectionPipeId _pipeId;

    private readonly List<IProjectionPayload> _pending = new();

    private bool _isConnected;

    public Task OnConnected()
    {
        _isConnected = true;
        return Task.CompletedTask;
    }

    public Task OnDisconnected()
    {
        _isConnected = false;
        return Task.CompletedTask;
    }

    public async Task ForceNotify()
    {
        if (_isConnected == false)
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

        if (_isConnected == false)
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

        if (_isConnected == false)
        {
            _logger.LogWarning("[User] [Projection] No connection service id found for {Id}", this.GetPrimaryKey());
            _pending.Add(payload);
            return Task.CompletedTask;
        }

        return Send(payload);
    }

    private Task Send(IProjectionPayload payload)
    {
        return _messaging.SendPipe(_pipeId, payload);
    }
}