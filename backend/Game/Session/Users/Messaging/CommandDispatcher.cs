using Common;
using Shared;

namespace Game;

public interface ICommandDispatcher
{
    Task Run(IReadOnlyLifetime lifetime, IUser user);
}

public class CommandDispatcher : ICommandDispatcher
{
    public CommandDispatcher(
        ICommandsCollection commands,
        ISessionUsers users,
        IExecutionQueue executionQueue)
    {
        _commands = commands;
        _users = users;
        _executionQueue = executionQueue;
    }

    private readonly ICommandsCollection _commands;
    private readonly ISessionUsers _users;
    private readonly IExecutionQueue _executionQueue;

    public async Task Run(IReadOnlyLifetime lifetime, IUser user)
    {
        var reader = user.Reader.Queue.Reader;
        var cancellation = lifetime.Token;

        var scope = new CommandScope()
        {
            Users = _users,
            User = user
        };

        while (await reader.WaitToReadAsync(cancellation))
        {
            while (reader.TryRead(out var request))
            {
                switch (request)
                {
                    case ServerEmptyRequest empty:
                    {
                        _executionQueue.Enqueue(() =>
                        {
                            var type = empty.Context.GetType();
                            _commands.EmptyCommands[type].Execute(scope, empty.Context);
                        });

                        break;
                    }
                    case ServerFullRequest full:
                    {
                        _executionQueue.Enqueue(() =>
                        {
                            var type = full.Context.GetType();
                            var result = _commands.FullCommands[type].Execute(scope, full.Context);
                            user.Send(result, full.RequestId);
                        });

                        break;
                    }
                }
            }
        }
    }
}