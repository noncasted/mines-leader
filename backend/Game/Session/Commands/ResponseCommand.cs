using Shared;

namespace Game;

public interface IResponseCommand
{
    Type RequestType { get; }

    INetworkContext Execute(IUser user, INetworkContext context);
}

public abstract class ResponseCommand<TContext, TResult> : IResponseCommand
    where TContext : INetworkContext
    where TResult : INetworkContext
{
    public Type RequestType { get; } = typeof(TContext);

    public INetworkContext Execute(IUser user, INetworkContext context)
    {
        var result = Execute(user, (TContext)context);
        return result;
    }

    protected abstract TResult Execute(IUser user, TContext context);
}