using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Orchestration;
using Orleans.Serialization;
using Orleans.Serialization.Configuration;
using Shared;
using Xunit;

namespace Tests.Messaging;

public class SharedTypesSerializationTests
{
    // Grain calls between gateway and silo serialize arguments; in-process test cluster calls don't,
    // so this guards the type allowlist for Shared types directly.
    [Fact]
    public void DeckDictionary_AsInterface_RoundTrips()
    {
        var services = new ServiceCollection()
            .AddSerializer()
            .Configure<TypeManifestOptions>(OrleansSetupExtensions.AllowSharedTypes)
            .BuildServiceProvider();

        var serializer = services.GetRequiredService<Serializer>();
        IReadOnlyDictionary<int, IReadOnlyList<CardType>> value =
            new Dictionary<int, IReadOnlyList<CardType>> { [0] = new List<CardType> { CardType.Trebuchet } };

        var bytes = serializer.SerializeToArray(value);
        var result = serializer.Deserialize<IReadOnlyDictionary<int, IReadOnlyList<CardType>>>(bytes);

        result[0].Should().Equal(CardType.Trebuchet);
    }
}
