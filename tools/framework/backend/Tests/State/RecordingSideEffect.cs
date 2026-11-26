using System.Collections.Concurrent;
using Common;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Infrastructure.Startup;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

[GenerateSerializer]
public class RecordingSideEffect : ISideEffect
{
    public static readonly ConcurrentDictionary<Guid, bool> Executions = new();

    [Id(0)] public Guid TrackingId { get; init; }

    public Task Execute(IOrleans orleans)
    {
        Executions[TrackingId] = true;
        return Task.CompletedTask;
    }
}
