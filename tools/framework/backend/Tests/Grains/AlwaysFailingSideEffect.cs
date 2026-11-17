using Common;
using Infrastructure;
using Infrastructure.State;
using Newtonsoft.Json;
using Orleans.Concurrency;
using Shared;

namespace Framework.Tests.Grains;

// Side effect that always fails — for max retry tests
[GenerateSerializer]
public class AlwaysFailingSideEffect : ISideEffect
{
    [Id(0)] public Guid TrackingId { get; init; }

    public Task Execute(IOrleans orleans)
    {
        throw new Exception("Always fails");
    }
}
