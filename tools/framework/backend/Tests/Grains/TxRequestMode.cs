using Infrastructure;
using Infrastructure.State;
using Orleans.Concurrency;

namespace Framework.Tests.Grains;

[GenerateSerializer]
public enum TxRequestMode
{
    Completed,
    Delayed,
    Faulted,
    ThrowsAtCall,
    ThrowsAfterDelay
}
