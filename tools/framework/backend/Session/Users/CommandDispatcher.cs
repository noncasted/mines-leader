using Internal;
using Shared;

namespace Game.Session;

public interface ICommandDispatcher
{
    void Run(IReadOnlyLifetime lifetime, IHostedUser user);
}

// The messages of the socket of a user go to the commands of the core in the session queue (ISessionCommandRunner:
// every request is answered, a failed one-way command is logged).
public class CommandDispatcher : ICommandDispatcher
{
    public CommandDispatcher(ISessionCommandRunner runner, IExecutionQueue executionQueue)
    {
        _runner = runner;
        _executionQueue = executionQueue;
    }

    private readonly ISessionCommandRunner _runner;
    private readonly IExecutionQueue _executionQueue;

    public void Run(IReadOnlyLifetime lifetime, IHostedUser user)
    {
        var reader = user.Socket.Reader;
        var writer = user.Socket.Writer;

        reader.OneWay.Advise(lifetime, HandleOneWay);
        reader.Requests.Advise(lifetime, HandleRequest);

        void HandleOneWay(OneWayMessageFromClient oneWay)
        {
            _executionQueue.Enqueue(() => _runner.RunOneWay(user, oneWay.Context));
        }

        void HandleRequest(RequestMessageFromClient request)
        {
            _executionQueue.Enqueue(() => {
                var result = _runner.RunRequest(user, request.Context, request.RequestId);
                writer.WriteResponse(result, request.RequestId);
            });
        }
    }
}
