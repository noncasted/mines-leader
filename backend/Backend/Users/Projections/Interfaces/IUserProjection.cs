using Infrastructure.Messaging;
using Orleans.Concurrency;

namespace Backend.Users;

public interface IUserProjection : IGrainWithGuidKey
{
    Task OnConnected();
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