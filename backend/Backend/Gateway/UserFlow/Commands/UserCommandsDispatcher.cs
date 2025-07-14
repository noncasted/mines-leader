using Common;
using Shared;

namespace Backend.Gateway;

public interface IUserCommandsDispatcher
{
    void Run(IUserSession session);
}

public class UserCommandsDispatcher : IUserCommandsDispatcher
{
    public UserCommandsDispatcher(IUserCommandsCollection commands)
    {
        _commands = commands;
    }

    private readonly IUserCommandsCollection _commands;

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
            return _commands.Entries[type].Execute(session, oneWay.Context);
        }

        async Task HandleRequest(RequestMessageFromClient request)
        {
            var type = request.Context.GetType();
            var result = await _commands.Entries[type].Execute(session, request.Context);
            await writer.WriteResponse(result, request.RequestId);
        }

        void HandleResponse(ResponseMessageFromClient response)
        {
            writer.OnRequestHandled(response.Context, response.RequestId);
        }
    }
}