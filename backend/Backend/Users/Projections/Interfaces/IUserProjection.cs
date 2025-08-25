using Orleans.Concurrency;

namespace Backend.Users;

public interface IUserProjection : IGrainWithGuidKey
{
    Task OnConnected(Guid connectionServiceId);
    Task OnDisconnected();

    [AlwaysInterleave]
    [Transaction(TransactionOption.Create)]
    Task ForceNotify();

    [AlwaysInterleave]
    [Transaction(TransactionOption.Join)]
    Task SendCached(IProjectionPayload payload);

    [AlwaysInterleave]
    [Transaction(TransactionOption.Join)]
    Task Cache(IProjectionPayload payload);
    
    Task SendOneTime(IProjectionPayload payload);
}