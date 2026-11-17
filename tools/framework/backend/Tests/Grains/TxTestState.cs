using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

[GenerateSerializer]
[GrainState(Table = "state_tx_test", State = "tx_test", Lookup = "TxTest", Key = GrainKeyType.Guid)]
public class TxTestState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public int Value { get; private set; }
    public int Version => 0;

    public static TxTestState Of(int value)
    {
        return new TxTestState { Value = value };
    }

    public void SetValue(int value)
    {
        Value = value;
    }

    public void Increment()
    {
        Value += 1;
    }
}
