using Infrastructure;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Meta.Users;

public interface IUserProjection : IGrainWithGuidKey
{
    [Transaction(TransactionOption.CreateOrJoin)]
    Task OnConnected();

    [Transaction(TransactionOption.CreateOrJoin)]
    Task OnDisconnected();

    [Transaction(TransactionOption.CreateOrJoin)]
    Task ForceNotify();

    [Transaction(TransactionOption.CreateOrJoin)]
    Task SendCached(IProjectionPayload payload);

    [Transaction(TransactionOption.CreateOrJoin)]
    Task Cache(IProjectionPayload payload);

    [Transaction(TransactionOption.CreateOrJoin)]
    Task SendOneTime(IProjectionPayload payload);
}

public class UserProjectionPipeId : IMessagePipeId
{
    public UserProjectionPipeId(Guid id)
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
        var state = await _state.Read();

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

        var state = await _state.Read();

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
        return _messaging.SendPipe(_pipeId, payload);
    }
}