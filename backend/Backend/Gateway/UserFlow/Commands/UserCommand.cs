using Shared;

namespace Backend.Gateway;

public interface IUserCommand
{
    Type RequestType { get; }

    Task<INetworkContext> Execute(IUserSession session, INetworkContext context);
}

public abstract class UserCommand<TRequest> : IUserCommand
    where TRequest : INetworkContext

{
    public Type RequestType => typeof(TRequest);

    public Task<INetworkContext> Execute(IUserSession session, INetworkContext context)
    {
        if (context is not TRequest request)
            throw new ArgumentException(
                $"Invalid request type: {context.GetType().Name}, expected: {typeof(TRequest).Name}");

        return Execute(session, request);
    }

    protected abstract Task<INetworkContext> Execute(IUserSession session, TRequest request);
}