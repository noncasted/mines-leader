using Common.Reactive;
using Infrastructure.State;

namespace Benchmarks;

public class BenchmarkStorage
{
    public BenchmarkStorage(IStateStorage stateStorage)
    {
        _stateStorage = stateStorage;
    }

    private readonly IStateStorage _stateStorage;

    public async Task<IReadOnlyList<BenchmarkState>> GetAll(string benchmarkName)
    {
        var lifetime = new Lifetime();
        var results = new List<BenchmarkState>();

        await foreach (var (_, state) in _stateStorage.ReadAll<Guid, BenchmarkState>(lifetime))
        {
            if (state.Name == benchmarkName)
                results.Add(state);
        }

        lifetime.Terminate();

        results.Sort((a, b) => b.Date.CompareTo(a.Date));
        return results;
    }

    public Task Write(BenchmarkState state)
    {
        var info = _stateStorage.Registry.Get<BenchmarkState>();

        var identity = new StateIdentity
        {
            Key = state.Id,
            Type = info.Name,
            TableName = info.TableName,
            Extension = null
        };

        return _stateStorage.Write(identity, state);
    }
}