using Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// The task a timeout left behind: nobody awaits it any more, so its late failure is only logged; unobserved, it would
// reach TaskScheduler.UnobservedTaskException.
internal static class LateTasks
{
    public static void Observe(Task task, ILogger logger, string message, params object?[] args)
    {
        task.ContinueWith(late => logger.LogDebug(late.Exception, message, args),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default)
            .NoAwait();
    }
}
