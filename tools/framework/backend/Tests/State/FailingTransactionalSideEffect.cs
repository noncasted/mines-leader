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
public class FailingTransactionalSideEffect : ITransactionalSideEffect
{
    [Id(0)] public required string Marker { get; init; }

    public Task Execute(IOrleans orleans)
    {
        throw new InvalidOperationException(Marker);
    }
}
