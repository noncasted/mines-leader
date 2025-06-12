using Shared;

namespace Game;

public abstract class ResponseCommand<TContext, TResult> : IResponseCommand
    where TContext : INetworkContext
    where TResult : INetworkContext
{
    public Type RequestType { get; } = typeof(TContext);

    public INetworkContext Execute(CommandScope scope, INetworkContext context)
    {
        var result = Execute(scope, (TContext)context);
        return result;
    }

    protected abstract TResult Execute(CommandScope scope, TContext context);
}