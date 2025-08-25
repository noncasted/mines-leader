using Common;
using Shared;

namespace Game;

public interface ICommandDispatcher
{
    void Run(IReadOnlyLifetime lifetime, IUser user);
}

public class CommandDispatcher : ICommandDispatcher
{
    public CommandDispatcher(ICommandsCollection commands, IExecutionQueue executionQueue)
    {
        _commands = commands;
        _executionQueue = executionQueue;
    }

    private readonly ICommandsCollection _commands;
    private readonly IExecutionQueue _executionQueue;

    public void Run(IReadOnlyLifetime lifetime, IUser user)
    {
        var reader = user.Connection.Reader;
        var writer = user.Connection.Writer;

        reader.OneWay.Advise(lifetime, HandleOneWay);
        reader.Requests.Advise(lifetime, HandleRequest);
        reader.Responses.Advise(lifetime, HandleResponse);

        void HandleOneWay(OneWayMessageFromClient oneWay)
        {
            _executionQueue.Enqueue(() =>
            {
                var type = oneWay.Context.GetType();
                var command = _commands.OneWay[type];
                command.Execute(user, oneWay.Context);
            });
        }

        void HandleRequest(RequestMessageFromClient request)
        {
            _executionQueue.Enqueue(() =>
            {
                var type = request.Context.GetType();
                var result = _commands.Responsible[type].Execute(user, request.Context);
                writer.WriteResponse(result, request.RequestId);
            });
        }

        void HandleResponse(ResponseMessageFromClient response)
        {
            writer.OnRequestHandled(response.Context, response.RequestId);
        }
    }
}