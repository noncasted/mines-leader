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
public class GatedSideEffect : ISideEffect
{
    [Id(0)] public Guid GateId { get; init; }

    public Task Execute(IOrleans orleans)
    {
        return TestGate.EnterAndWait(GateId);
    }
}
