using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

public interface ISimpleTestGrain : IGrainWithGuidKey
{
    Task SetCounter(int value);
    Task<int> GetCounter();
    Task SetLabel(string label);
    Task<string> GetLabel();
}

public class SimpleTestGrain : Grain, ISimpleTestGrain
{
    public SimpleTestGrain([State] State<SimpleTestState> state)
    {
        _state = state;
    }

    private readonly State<SimpleTestState> _state;

    public async Task SetCounter(int value)
    {
        await _state.Write(s => s.SetCounter(value));
    }

    public async Task<int> GetCounter()
    {
        return await _state.Read(s => s.Counter);
    }

    public async Task SetLabel(string label)
    {
        await _state.Write(s => s.SetLabel(label));
    }

    public async Task<string> GetLabel()
    {
        return await _state.Read(s => s.Label);
    }
}
