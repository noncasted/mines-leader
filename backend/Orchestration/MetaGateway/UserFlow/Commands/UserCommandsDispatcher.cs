using Common.Extensions;
using MetaGateway.UserFlow.Connection;
using Shared;

namespace MetaGateway.UserFlow.Commands;

public interface IUserCommandsDispatcher
{
    void Run(IUserSession session);
}

public class UserCommandsDispatcher : IUserCommandsDispatcher
{
    public UserCommandsDispatcher(IUserCommandsCollection commands, ILogger<UserCommandsDispatcher> logger)
    {
        _commands = commands;
        _logger = logger;
    }

    private readonly IUserCommandsCollection _commands;
    private readonly ILogger<UserCommandsDispatcher> _logger;

    public void Run(IUserSession session)
    {
        var reader = session.Connection.Reader;
        var writer = session.Connection.Writer;

        reader.OneWay.Advise(session.Lifetime, oneWay => HandleOneWay(oneWay).NoAwait());
        reader.Requests.Advise(session.Lifetime, request => HandleRequest(request).NoAwait());
        reader.Responses.Advise(session.Lifetime, HandleResponse);

        async Task HandleOneWay(OneWayMessageFromClient oneWay)
        {
            var type = oneWay.Context.GetType();

            if (_commands.Entries.TryGetValue(type, out var command) == false)
            {
                _logger.LogError("[User] [Command] No command for one-way {CommandType} of user {UserId}",
                    type.FullName,
                    session.UserId);

                return;
            }

            try
            {
                await command.Execute(session, oneWay.Context);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[User] [Command] One-way command {CommandType} of user {UserId} failed",
                    type.FullName,
                    session.UserId);
            }
        }

        // Every request is answered: the result, or ErrorResponse when there is no command for its type or the
        // command throws - the client turns it into an exception instead of waiting.
        async Task HandleRequest(RequestMessageFromClient request)
        {
            var context = request.Context;
            var type = context.GetType();

            if (_commands.Entries.TryGetValue(type, out var command) == false)
            {
                _logger.LogError("[User] [Command] No command for request {CommandType} of user {UserId}, request {RequestId}",
                    type.FullName,
                    session.UserId,
                    request.RequestId);

                writer.WriteResponse(ErrorResponse.UnknownCommand(context), request.RequestId);
                return;
            }

            INetworkContext result;

            try
            {
                result = await command.Execute(session, context);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[User] [Command] Request command {CommandType} of user {UserId} failed, request {RequestId}",
                    type.FullName,
                    session.UserId,
                    request.RequestId);

                result = ErrorResponse.Create(Shared.ErrorCode.Internal, context, e);
            }

            writer.WriteResponse(result, request.RequestId);
        }

        void HandleResponse(ResponseMessageFromClient response)
        {
            writer.OnRequestHandled(response.Context, response.RequestId);
        }
    }
}