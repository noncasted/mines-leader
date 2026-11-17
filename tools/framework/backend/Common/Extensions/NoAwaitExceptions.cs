namespace Common.Extensions;

// The failure of a task nobody awaits (a background loop, a fire-and-forget call) has no caller to reach: NoAwait
// hands it to Handler at the moment the task fails. ClusterParticipantStartup installs a logging one when the host has
// none; without a handler the failure goes to the standard error, never only to an unobserved task.
public static class NoAwaitExceptions
{
    public static Action<Exception>? Handler { get; private set; }

    // Null removes the handler.
    public static void SetHandler(Action<Exception>? handler)
    {
        Handler = handler;
    }

    internal static void Report(Exception exception)
    {
        var handler = Handler;

        if (handler == null)
        {
            Console.Error.WriteLine($"[NoAwait] Unobserved task failed: {exception}");
            return;
        }

        handler(exception);
    }
}
