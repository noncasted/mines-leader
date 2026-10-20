using Common.Reactive;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Session;

public interface ICommandDispatcher
{
    void Run(IReadOnlyLifetime lifetime, IUser user);
}

// Every request is answered: the result of its command, or ErrorResponse when there is no command for its type, the
// session refuses it (SessionRequestException) or the command throws (the client turns it into an exception instead
// of waiting forever). A failed one-way command is logged with its context; the session goes on.
public class CommandDispatcher : ICommandDispatcher
{
    public CommandDispatcher(ICommandsCollection commands, IExecutionQueue executionQueue, ILogger logger)
    {
        _commands = commands;
        _executionQueue = executionQueue;
        _logger = logger;
    }

    private readonly ICommandsCollection _commands;
    private readonly IExecutionQueue _executionQueue;
    private readonly ILogger _logger;

    public void Run(IReadOnlyLifetime lifetime, IUser user)
    {
        var reader = user.Connection.Reader;
        var writer = user.Connection.Writer;

        reader.OneWay.Advise(lifetime, HandleOneWay);
        reader.Requests.Advise(lifetime, HandleRequest);
        reader.Responses.Advise(lifetime, HandleResponse);

        void HandleOneWay(OneWayMessageFromClient oneWay)
        {
            _executionQueue.Enqueue(() => {
                var type = oneWay.Context.GetType();

                if (_commands.OneWay.TryGetValue(type, out var command) == false)
                {
                    _logger.LogError("[Session] [Command] No one-way command for {Type} from user {Index} {UserId}",
                        type.FullName,
                        user.Index,
                        user.Id);

                    return;
                }

                try
                {
                    command.Execute(user, oneWay.Context);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "[Session] [Command] One-way command {Type} of user {Index} {UserId} failed",
                        type.FullName,
                        user.Index,
                        user.Id);
                }
            });
        }

        void HandleRequest(RequestMessageFromClient request)
        {
            _executionQueue.Enqueue(() => {
                var context = request.Context;
                var type = context.GetType();

                if (_commands.Responsible.TryGetValue(type, out var command) == false)
                {
                    _logger.LogError(
                        "[Session] [Command] No request command for {Type} from user {Index} {UserId}, request {RequestId}",
                        type.FullName,
                        user.Index,
                        user.Id,
                        request.RequestId);

                    writer.WriteResponse(ErrorResponse.UnknownCommand(context), request.RequestId);
                    return;
                }

                INetworkContext result;

                try
                {
                    result = command.Execute(user, context);
                }
                catch (SessionRequestException e)
                {
                    _logger.LogWarning(
                        "[Session] [Command] Request {Type} of user {Index} {UserId} refused ({Code}): {Message}, request {RequestId}",
                        type.FullName,
                        user.Index,
                        user.Id,
                        e.Code,
                        e.Message,
                        request.RequestId);

                    result = ErrorResponse.Create(e.Code, context, e);
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        "[Session] [Command] Request command {Type} of user {Index} {UserId} failed, request {RequestId}",
                        type.FullName,
                        user.Index,
                        user.Id,
                        request.RequestId);

                    result = ErrorResponse.Create(Shared.ErrorCode.Internal, context, e);
                }

                writer.WriteResponse(result, request.RequestId);
            });
        }

        void HandleResponse(ResponseMessageFromClient response)
        {
            writer.OnRequestHandled(response.Context, response.RequestId);
        }
    }
}
