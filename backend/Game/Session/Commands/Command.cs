using Shared;

namespace Game;

public interface ICommand
{
    Type RequestType { get; }

    void Execute(IUser user, INetworkContext context);
}

public abstract class Command<TContext> : ICommand where TContext : INetworkContext
{
    public Type RequestType { get; } = typeof(TContext);

    public void Execute(IUser user, INetworkContext context)
    {
        Execute(user, (TContext)context);
    }

    protected abstract void Execute(IUser user, TContext context);
}