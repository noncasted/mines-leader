using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using NSubstitute;
using Orleans.Serialization;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// A session that did not close on the client, as its next session reports it: kept with the new one and found from the old one.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryPreviousSessionTests : ClientTelemetryStorageTest
{
    public ClientTelemetryPreviousSessionTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    private readonly IClientTelemetrySwitch _switch = Substitute.For<IClientTelemetrySwitch>();

    private ClientTelemetryConnection Open()
    {
        _switch.IsEnabled.Returns(true);

        return new ClientTelemetryConnections(_switch, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance).Of(TestLifetime.Child());
    }

    private static SharedClientTelemetry.PreviousSession Previous(Guid id)
    {
        return new SharedClientTelemetry.PreviousSession
        {
            SessionId = id,
            Tags = ["menu"],
            LastSampleTime = 31_000,
            SinceMs = 4_000,
            Marks =
            [
                new SharedClientTelemetry.Mark { Time = 30_500, Name = "webgl.error.out_of_memory" },
                new SharedClientTelemetry.Mark { Time = 31_200, Name = "page.pagehide" }
            ],
            SampleNames = ["frame.dt.max", "web.raf.dt.max"],
            Samples =
            [
                new SharedClientTelemetry.Sample { Time = 30_000, Values = [41.5, double.NaN] },
                new SharedClientTelemetry.Sample { Time = 31_000, Values = [250, 260], Hidden = true }
            ]
        };
    }

    [Fact]
    public async Task ThePreviousSession_IsKeptWithTheNewOne_AndFindsIt()
    {
        var connection = Open();
        var lost = (await connection.Start(new SharedClientTelemetry.SessionStart())).SessionId;
        var next = (await Open().Start(new SharedClientTelemetry.SessionStart { Previous = Previous(lost) })).SessionId;

        var previous = (await Session(next)).Previous!;

        previous.SessionId.Should().Be(lost);
        previous.Tags.Should().Equal("menu");
        previous.LastSampleTime.Should().Be(31_000);
        previous.SinceMs.Should().Be(4_000);
        previous.Marks.Select(mark => (mark.Time, mark.Name)).Should().Equal((30_500L, "webgl.error.out_of_memory"), (31_200L, "page.pagehide"));
        previous.Samples.Select(sample => (sample.Time, sample.Hidden)).Should().Equal((30_000L, false), (31_000L, true));
        previous.Samples[0].Values.Should().BeEquivalentTo(new Dictionary<string, double> { ["frame.dt.max"] = 41.5 });
        previous.Samples[1].Values.Should().BeEquivalentTo(new Dictionary<string, double> { ["frame.dt.max"] = 250, ["web.raf.dt.max"] = 260 });
        (await Session(lost)).Previous.Should().BeNull();

        var found = await Reader.GetSessions(new TelemetrySessionFilter { PreviousId = lost });
        found.Select(item => item.Id).Should().Equal(next);
    }

    // A grain call takes it through the serializer of Orleans and the copier, the row through the json of the states.
    [Fact]
    public void TheSamples_SurviveTheSerializers()
    {
        var previous = TelemetryPreviousParser.Parse(Previous(Guid.NewGuid())).Session!;
        var silo = Cluster.Cluster.Silos[0].ServiceProvider;
        var serializer = silo.GetRequiredService<Serializer>();
        var copier = silo.GetRequiredService<DeepCopier>();
        var state = new TelemetrySessionState();
        state.Open(Guid.NewGuid(), new TelemetryDevice(), [], null, previous, DateTime.UtcNow);
        var settings = JsonStateSettings.CreateBase();

        serializer.Deserialize<TelemetryPreviousSession>(serializer.SerializeToArray(previous)).Should().BeEquivalentTo(previous);
        copier.Copy(previous).Should().BeEquivalentTo(previous);
        JsonConvert.DeserializeObject<TelemetrySessionState>(JsonConvert.SerializeObject(state, settings), settings)!.Previous.Should().BeEquivalentTo(previous);
    }

    [Fact]
    public void ARowWrittenBeforeTheSamples_ReadsAsNoSamples()
    {
        var settings = JsonStateSettings.CreateBase();
        var row = JsonConvert.DeserializeObject<TelemetrySessionState>(
            """{ "IsStarted": true, "Previous": { "SessionId": "11111111-1111-1111-1111-111111111111", "Tags": [], "LastSampleTime": 31000, "SinceMs": null, "Marks": [] } }""",
            settings)!;

        row.Previous!.Samples.Should().BeEmpty();
    }
}
