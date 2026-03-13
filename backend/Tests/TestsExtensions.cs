namespace Tests;

public static class TestsExtensions
{
    public static Task RunConcurrentIterations(
        this ClusterTestNodeHandle handle,
        IConcurrentIterationTestPayload payload,
        Func<Task> action)
    {
        return handle.RunConcurrentIterations(payload.Iterations, payload.Concurrent, action);
    }
    
    public static async Task RunConcurrentIterations(
        this ClusterTestNodeHandle handle,
        int iterations,
        int concurrent,
        Func<Task> action)
    {
        for (var i = 0; i < iterations; i++)
        {
            var tasks = new List<Task>();

            for (var c = 0; c < concurrent; c++)
                tasks.Add(action());

            await Task.WhenAll(tasks);

            handle.Progress.SetProgress((float)(i + 1) / iterations);
            handle.Progress.Log($"Processed {i + 1}/{iterations}");
        }
    }
}