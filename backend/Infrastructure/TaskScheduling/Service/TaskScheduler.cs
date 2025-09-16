namespace Infrastructure.TaskScheduling;

public interface ITaskScheduler
{
    void Schedule(IPriorityTask task);
}

public class TaskScheduler : ITaskScheduler
{
    public void Schedule(IPriorityTask task)
    {
        
    }
}