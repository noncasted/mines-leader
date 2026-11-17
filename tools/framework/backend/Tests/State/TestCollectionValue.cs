using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.State;

[GenerateSerializer]
public class TestCollectionValue : IDirectStateValue
{
    [Id(0)] public Guid Id { get; init; } = Guid.NewGuid();
    [Id(1)] public string Name { get; init; } = string.Empty;
    public int Version => 0;
}
