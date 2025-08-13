using Common;
using Microsoft.Extensions.Logging;
using Shared;

namespace Backend.Gateway;

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

        Task HandleOneWay(OneWayMessageFromClient oneWay)
        {
            var type = oneWay.Context.GetType();

            try
            {
                return _commands.Entries[type].Execute(session, oneWay.Context);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[User] [Command] Error handling one-way command: {CommandType}", type.Name);
                return Task.CompletedTask;
            }
        }

        async Task HandleRequest(RequestMessageFromClient request)
        {
            var type = request.Context.GetType();

            try
            {
                var result = await _commands.Entries[type].Execute(session, request.Context);
                await writer.WriteResponse(result, request.RequestId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[User] [Command] Error handling request command: {CommandType}", type.Name);
            }
        }

        void HandleResponse(ResponseMessageFromClient response)
        {
            writer.OnRequestHandled(response.Context, response.RequestId);
        }
    }
}