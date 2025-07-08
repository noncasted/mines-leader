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
        session.Connection.Reader.Received.Advise(session.Lifetime, request =>
        {
            if (request is not ServerFullRequest full)
                throw new ArgumentException("Expected ServerFullRequest", nameof(request));

            HandleCommand(full).NoAwait();
        });

        async Task HandleCommand(ServerFullRequest request)
        {
            var type = request.Context.GetType();
            var result = await _commands.Entries[type].Execute(session, request.Context);
            await session.Connection.Writer.WriteFull(result, request.RequestId);
        }
    }
}