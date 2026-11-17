using System.Collections.Concurrent;
using Common;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.Startup;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

[GenerateSerializer]
public class SlowSideEffect : ISideEffect
{
    public static readonly ConcurrentDictionary<Guid, int> Executions = new();
    public static readonly ConcurrentDictionary<Guid, bool> Finished = new();

    [Id(0)] public Guid TrackingId { get; init; }

    public async Task Execute(IOrleans orleans)
    {
        Executions.AddOrUpdate(TrackingId, 1, (_, count) => count + 1);
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        Finished[TrackingId] = true;
    }
}
