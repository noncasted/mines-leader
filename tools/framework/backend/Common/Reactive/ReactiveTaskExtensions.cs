namespace Internal;

// The Task waits over the reactive primitives of Framework.Common (the Unity client has the UniTask ones).
// A wait subscribes for a child lifetime of its own, ended when the wait completes: a completed wait leaves nothing on
// a long lifetime. The awaiters run asynchronously, never inside the Invoke / Set / Terminate that completed the wait.
public static class ReactiveTaskExtensions
{
    // Completes on the first invoke, is cancelled when the lifetime ends first.
    public static Task WaitInvoke(this IEventSource source, IReadOnlyLifetime lifetime)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitLifetime = lifetime.Child();
        waitLifetime.Listen(() => completion.TrySetCanceled());

        source.Advise(waitLifetime, () => {
            completion.TrySetResult();
            waitLifetime.Terminate();
        });

        return completion.Task;
    }

    public static Task<T> WaitInvoke<T>(this IEventSource<T> source, IReadOnlyLifetime lifetime)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitLifetime = lifetime.Child();
        waitLifetime.Listen(() => completion.TrySetCanceled());

        source.Advise(waitLifetime, value => {
            completion.TrySetResult(value);
            waitLifetime.Terminate();
        });

        return completion.Task;
    }

    public static Task WaitFalse(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
    {
        return property.Wait(lifetime, false);
    }

    public static Task WaitTrue(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime)
    {
        return property.Wait(lifetime, true);
    }

    private static Task Wait(this ILifetimedValue<bool> property, IReadOnlyLifetime lifetime, bool expected)
    {
        if (property.Value == expected)
            return Task.CompletedTask;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitLifetime = lifetime.Child();
        waitLifetime.Listen(() => completion.TrySetCanceled());

        property.Advise(waitLifetime, (_, value) => {
            if (value != expected)
                return;

            completion.TrySetResult();
            waitLifetime.Terminate();
        });

        return completion.Task;
    }
}
