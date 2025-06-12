using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;

namespace Game;

public interface IExecutionQueue
{
    void Enqueue(Action action);
}

public class ExecutionQueue : IExecutionQueue
{
    public ExecutionQueue(ILogger<ExecutionQueue> logger)
    {
        _commandQueue = new ActionBlock<Action>(command =>
        {
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