using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

public interface ICollectionTestGrain : IGrainWithGuidKey
{
    Task SetName(string name);
    Task<string> GetName();
}

public class CollectionTestGrain : Grain, ICollectionTestGrain
{
    public CollectionTestGrain([State] State<CollectionTestState> state)
    {
        _state = state;
    }

    private readonly State<CollectionTestState> _state;

    public async Task SetName(string name)
    {
        await _state.Write(s => s.Init(this.GetGrainId().GetGuidKey(), name));
    }

    public async Task<string> GetName()
    {
        return await _state.Read(s => s.Name);
    }
}
