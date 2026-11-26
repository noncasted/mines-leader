using Internal;

namespace Infrastructure;

// Refreshes the observers of a client until the lifetime ends. Every listener runs on its own: one that fails or hangs
// does not stop the others, and a hung one is not called again until its call ends. A pass waits for the listeners at
// most the timeout, and the shortest delay among them is the sleep until the next pass.
internal static class ResubscribeLoop
{
    // The response timeout of an Orleans call: a resubscribe still running after it is held by something else.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static async Task Run(
        IReadOnlyLifetime lifetime,
        Func<IReadOnlyCollection<IResubscribingListener>> listeners,
        Func<TimeSpan> idleDelay,
        Func<TimeSpan> timeout)
    {
        var running = new Dictionary<IResubscribingListener, Task>();

        while (lifetime.IsTerminated == false)
        {
            var snapshot = listeners();
            var current = snapshot.ToHashSet();

            foreach (var (listener, task) in running.ToArray())
            {
                if (task.IsCompleted == true || current.Contains(listener) == false)
                    running.Remove(listener);
            }

            foreach (var listener in snapshot)
            {
                if (running.ContainsKey(listener) == false)
                    running[listener] = Refresh(listener);
            }

            try
            {
                await WaitForPass(running, timeout(), lifetime);

                var delay = snapshot.Count == 0
                    ? idleDelay()
                    : snapshot.Min(l => l.Interval.GetNextDelay());

                await Task.Delay(delay, lifetime.Token);
            }
            catch (OperationCanceledException) when (lifetime.IsTerminated)
            {
                // The end of the lifetime is the end of the loop.
            }
        }
    }

    private static async Task WaitForPass(
        Dictionary<IResubscribingListener, Task> running,
        TimeSpan timeout,
        IReadOnlyLifetime lifetime)
    {
        try
        {
            await Task.WhenAll(running.Values).WaitAsync(timeout, lifetime.Token);
        }
        catch (TimeoutException)
        {
            foreach (var (listener, task) in running)
            {
                if (task.IsCompleted == false)
                    listener.RecordFailure(new TimeoutException($"Resubscribe is still running after {timeout}"));
            }
        }
    }

    private static async Task Refresh(IResubscribingListener listener)
    {
        try
        {
            await listener.Resubscribe();
            listener.RecordSuccess();
        }
        catch (Exception e)
        {
            listener.RecordFailure(e);
        }
    }
}
