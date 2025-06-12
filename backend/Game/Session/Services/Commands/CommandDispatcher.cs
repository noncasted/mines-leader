using Common;
using Shared;

namespace Game;

public class CommandDispatcher : ICommandDispatcher
{
    public CommandDispatcher(
        ICommandsCollection commands,
        IUser user,
        ISession session,
        IExecutionQueue executionQueue)
    {
        _commands = commands;
        _user = user;
        _session = session;
        _executionQueue = executionQueue;
    }

    private readonly ICommandsCollection _commands;
    private readonly IUser _user;
    private readonly ISession _session;
    private readonly IExecutionQueue _executionQueue;

    public async Task Run(IReadOnlyLifetime lifetime)
    {
        var reader = _user.Reader.Queue.Reader;
        var cancellation = lifetime.Token;

        var scope = new CommandScope()
        {
            Session = _session,
            User = _user
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
                            _user.Send(result, full.RequestId);
                        });

                        break;
                    }
                }
            }
        }
    }
}