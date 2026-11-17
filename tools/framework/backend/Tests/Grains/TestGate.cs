using System.Collections.Concurrent;

namespace Framework.Tests.Grains;

// Holds a call of a test grain until the test releases it. The cluster of the tests lives in this process, so the
// grain finds the gate by its id.
public sealed class TestGate
{
    private static readonly ConcurrentDictionary<Guid, TestGate> Gates = new();

    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Guid Id { get; } = Guid.NewGuid();
    public Task Entered => _entered.Task;

    public static TestGate Create()
    {
        var gate = new TestGate();
        Gates[gate.Id] = gate;
        return gate;
    }

    public static Task EnterAndWait(Guid id)
    {
        var gate = Gates[id];
        gate._entered.SetResult();
        return gate._released.Task;
    }

    public void Release()
    {
        _released.SetResult();
        Gates.TryRemove(Id, out _);
    }
}
