using Common.Network;
using Internal;
using Game.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Framework.Tests.Session;

// Single consumer at a time, like the real ExecutionQueue, but pumped by the test. Actions run without a
// synchronization context: an await inside the session (SessionUserRuns.Run) continues inline when its task completes,
// not on a context of the test runner.
public class ManualExecutionQueue : IExecutionQueue
{
    private readonly Queue<Action> _actions = new();
    private readonly Lock _runLock = new();

    public int Count
    {
        get
        {
            lock (_actions)
                return _actions.Count;
        }
    }

    public void Enqueue(Action action)
    {
        lock (_actions)
            _actions.Enqueue(action);
    }

    public bool RunNext()
    {
        lock (_runLock)
        {
            Action? action;

            lock (_actions)
            {
                if (_actions.TryDequeue(out action) == false)
                    return false;
            }

            var context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                action();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }

            return true;
        }
    }

    public void Drain()
    {
        while (RunNext() == true)
        {
        }
    }
}
