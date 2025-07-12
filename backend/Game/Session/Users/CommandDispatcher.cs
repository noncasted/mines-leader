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
        user.Reader.Received.Advise(lifetime, request =>
        {
            switch (request)
            {
                case OneWayMessageFromClient empty:
                {
                    _executionQueue.Enqueue(() =>
                    {
                        var type = empty.Context.GetType();
                        _commands.EmptyCommands[type].Execute(user, empty.Context);
                    });

                    break;
                }
                case ResponsibleMessageFromClient full:
                {
                    _executionQueue.Enqueue(() =>
                    {
                        var type = full.Context.GetType();
                        var result = _commands.FullCommands[type].Execute(user, full.Context);
                        user.Send(result, full.RequestId);
                    });

                    break;
                }
            }
        });
    }
}