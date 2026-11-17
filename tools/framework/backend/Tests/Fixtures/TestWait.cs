using Xunit;

namespace Framework.Tests.Fixtures;

// Waits for an asynchronous effect by its condition, with an upper bound for a loaded machine: a passing test takes
// as long as the effect, a failing one throws at the bound.
public static class TestWait
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    public static Task Until(Func<bool> condition)
    {
        return Until(() => Task.FromResult(condition()));
    }

    public static async Task Until(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + Bound;

        while (await condition() == false)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Condition not met in {Bound}");

            await Task.Delay(PollInterval, TestContext.Current.CancellationToken);
        }
    }
}
