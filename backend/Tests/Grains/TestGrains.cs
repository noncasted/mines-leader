using Infrastructure;
using Infrastructure.State;

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

    Task<int> Get();
    Task Deactivate();
}

public class TxTestGrain : Grain, ITxTestGrain {
    public TxTestGrain([State] State<TxTestState> state) {
        _state = state;
    }

    private readonly State<TxTestState> _state;

    public Task Increment() {
        return _state.Write(s => s.Value += 1);
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
