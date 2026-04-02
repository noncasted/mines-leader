using Infrastructure;
using Infrastructure.State;
using Orleans.Concurrency;

namespace Tests.Grains;

// --- Simple state test grain ---

[GenerateSerializer]
public class SimpleTestState : IStateValue {
    [Id(0)] public int Counter { get; set; }
    [Id(1)] public string Label { get; set; } = string.Empty;
    public int Version => 0;
}

public interface ISimpleTestGrain : IGrainWithGuidKey {
    Task SetCounter(int value);
    Task<int> GetCounter();
    Task SetLabel(string label);
    Task<string> GetLabel();
}

public class SimpleTestGrain : Grain, ISimpleTestGrain {
    public SimpleTestGrain([State] State<SimpleTestState> state) {
        _state = state;
    }

    private readonly State<SimpleTestState> _state;

    public async Task SetCounter(int value) {
        await _state.Write(s => s.Counter = value);
    }

    public async Task<int> GetCounter() {
        return await _state.Read(s => s.Counter);
    }

    public async Task SetLabel(string label) {
        await _state.Write(s => s.Label = label);
    }

    public async Task<string> GetLabel() {
        return await _state.Read(s => s.Label);
    }
}

// --- Transaction test grain ---

[GenerateSerializer]
public class TxTestState : IStateValue {
    [Id(0)] public int Value { get; set; }
    public int Version => 0;
}

public interface ITxTestGrain : IGrainWithGuidKey {
    [Transaction]
    Task Increment();

    [Transaction]
    Task IncrementWithDelay(int delayMs);

    Task<int> Get();
    Task Deactivate();
}

[Reentrant]
public class TxTestGrain : Grain, ITxTestGrain {
    public TxTestGrain([State] State<TxTestState> state) {
        _state = state;
    }

    private readonly State<TxTestState> _state;

    public Task Increment() {
        return _state.Write(s => s.Value += 1);
    }

    public async Task IncrementWithDelay(int delayMs) {
        await _state.Write(s => s.Value += 1);
        await Task.Delay(delayMs);
    }

    public async Task<int> Get() {
        return await _state.Read(s => s.Value);
    }

    public Task Deactivate() {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}

// --- Side effect test grain ---

[GenerateSerializer]
public class TestSideEffect : ISideEffect {
    [Id(0)] public Guid TargetGrainId { get; set; }

    public async Task Execute(IOrleans orleans) {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());
        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }
}

// Transactional side effect — executes inside transaction, deletion atomic with commit
[GenerateSerializer]
public class TransactionalTestSideEffect : ITransactionalSideEffect {
    [Id(0)] public Guid TargetGrainId { get; set; }

    public async Task Execute(IOrleans orleans) {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        await grain.Increment();
    }
}

// Side effect that fails N times before succeeding
[GenerateSerializer]
public class FailingTestSideEffect : ISideEffect {
    [Id(0)] public Guid TargetGrainId { get; set; }
    [Id(1)] public int FailCount { get; set; }

    // Static counter shared across executions (keyed by TargetGrainId to avoid cross-test interference)
    private static readonly Dictionary<Guid, int> Attempts = new();

    public async Task Execute(IOrleans orleans) {
        lock (Attempts) {
            Attempts.TryGetValue(TargetGrainId, out var count);
            Attempts[TargetGrainId] = count + 1;
            if (count < FailCount)
                throw new Exception($"Intentional failure {count + 1}/{FailCount}");
        }

        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());
        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }

    public static void ResetAttempts() {
        lock (Attempts) { Attempts.Clear(); }
    }

    public static int GetAttemptCount(Guid targetId) {
        lock (Attempts) { return Attempts.GetValueOrDefault(targetId); }
    }
}

// Side effect that always fails — for max retry tests
[GenerateSerializer]
public class AlwaysFailingSideEffect : ISideEffect {
    [Id(0)] public Guid TrackingId { get; set; }

    public Task Execute(IOrleans orleans) {
        throw new Exception("Always fails");
    }
}

public interface ISideEffectTestGrain : IGrainWithGuidKey {
    Task RegisterSideEffect(Guid targetId);
}

public class SideEffectTestGrain : Grain, ISideEffectTestGrain {
    public SideEffectTestGrain(
        [State] State<TxTestState> state,
        ISideEffectsStorage sideEffectsStorage) {
        _state = state;
        _sideEffectsStorage = sideEffectsStorage;
    }

    private readonly State<TxTestState> _state;
    private readonly ISideEffectsStorage _sideEffectsStorage;

    public async Task RegisterSideEffect(Guid targetId) {
        await _sideEffectsStorage.Write(new TestSideEffect { TargetGrainId = targetId });
    }
}
