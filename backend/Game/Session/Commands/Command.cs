using Shared;

namespace Game;

public abstract class Command<TContext> : ICommand
    where TContext : INetworkContext
{
    public Type RequestType { get; } = typeof(TContext);

    public void Execute(CommandScope scope, INetworkContext context)
    {
        Execute(scope, (TContext)context);
    }

    protected abstract void Execute(CommandScope scope, TContext context);
}