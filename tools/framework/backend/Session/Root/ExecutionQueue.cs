using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace Game.Session;

public interface IExecutionQueue
{
    void Enqueue(Action action);
}

public class ExecutionQueue : IExecutionQueue
{
    public ExecutionQueue(ILogger<ExecutionQueue> logger)
    {
        _commandQueue = new ActionBlock<Action>(command => {
            try
            {
                command();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error executing command in session");
            }
        }, new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 1,
            EnsureOrdered = true
        });
    }

    private readonly ActionBlock<Action> _commandQueue;

    public void Enqueue(Action action)
    {
        _commandQueue.Post(action);
    }
}
public static class ExecutionQueueExtensions
{
    // The action runs in the queue; the task completes (or fails) with it, from any thread.
    public static Task Run(this IExecutionQueue queue, Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        queue.Enqueue(() => {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        });

        return completion.Task;
    }

    // The services of the container of the session (IServiceFactory.Setup) in the session queue, where the users, the
    // owner and the objects they subscribe to live. The factory of the game calls it right after the session is built.
    public static Task SetupServices(this IExecutionQueue queue, IServiceFactory factory)
    {
        return queue.Run(factory.Setup);
    }
}
