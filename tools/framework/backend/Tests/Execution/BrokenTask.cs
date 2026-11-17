using Infrastructure.Execution;

namespace Framework.Tests.Execution;

// A task whose members fail as a broken implementation does: its Delay throws, or its Id throws from a given read on.
public class BrokenTask : IPriorityTask
{
    public BrokenTask(int idReadsBeforeFailure, bool delayFails)
    {
        _idReadsBeforeFailure = idReadsBeforeFailure;
        _delayFails = delayFails;
    }

    public const string Failure = "broken task";

    private readonly int _idReadsBeforeFailure;
    private readonly bool _delayFails;
    private int _idReads;

    public TaskPriority Priority => TaskPriority.Medium;

    public string Id
    {
        get
        {
            if (Interlocked.Increment(ref _idReads) > _idReadsBeforeFailure)
                throw new InvalidOperationException(Failure);

            return "broken";
        }
    }

    public TimeSpan Delay => _delayFails ? throw new InvalidOperationException(Failure) : TimeSpan.Zero;

    public Task Execute()
    {
        return Task.CompletedTask;
    }
}
