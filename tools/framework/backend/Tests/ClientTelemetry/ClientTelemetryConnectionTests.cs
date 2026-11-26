using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The handling of one socket over the real grain and database, without a socket: its lifetime is enough.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryConnectionTests : ClientTelemetryStorageTest
{
    public ClientTelemetryConnectionTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    private readonly IClientTelemetrySwitch _switch = Substitute.For<IClientTelemetrySwitch>();

    private static SharedClientTelemetry.SessionStart Start(params string[] tags)
    {
        return new SharedClientTelemetry.SessionStart { Device = new SharedClientTelemetry.Device { Platform = "web" }, Tags = tags };
    }

    private (ClientTelemetryConnection Connection, Lifetime Lifetime) Open(bool flag = true)
    {
        _switch.IsEnabled.Returns(flag);

        var lifetime = new Lifetime(TestLifetime);
        var connections = new ClientTelemetryConnections(_switch, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance);

        return (connections.Of(lifetime), lifetime);
    }

    private static SharedClientTelemetry.Batch OneSample(Guid session)
    {
        return TelemetryWire.Batch(session, ["frame.dt"], [(1000, [10], false)]);
    }

    [Fact]
    public async Task TheEndOfTheLifetime_WritesTheSession_AsLost_WithItsData()
    {
        var (connection, lifetime) = Open();
        var id = (await connection.Start(Start("run"))).SessionId;
        (await connection.Accept(OneSample(id))).Enabled.Should().BeTrue();

        lifetime.Terminate();

        await TestWait.Until(async () => (await Reader.GetSession(id))?.Status == TelemetrySessionStatus.Lost);

        var session = await Session(id);
        session.Digest!.Summary.Samples.Should().Be(1);
        session.Tags.Should().Equal("run");
        (await Reader.GetSamples(id, 0, 10)).Single().Values.Should().Equal(10);
        (await RawState("state_client_telemetry_logs", id)).Should().NotBeNull();

        (await connection.Start(Start())).Enabled.Should().BeFalse("a socket that is gone starts nothing");
    }

    [Fact]
    public async Task MenuThenRunThenMenu_EachIsWrittenWithItsTags()
    {
        var (connection, lifetime) = Open();
        var menu = (await connection.Start(Start("menu"))).SessionId;
        await connection.Accept(OneSample(menu));

        var run = (await connection.Start(Start("run", "stage.first"))).SessionId;
        run.Should().NotBe(menu);

        (await Session(menu)).Status.Should().Be(TelemetrySessionStatus.Ended);
        (await connection.Accept(OneSample(menu))).Enabled.Should().BeFalse("not the session of this socket any more");
        (await connection.Accept(OneSample(run))).Enabled.Should().BeTrue();

        var again = (await connection.Start(Start("menu"))).SessionId;
        await connection.End(new SharedClientTelemetry.SessionEnd { SessionId = again });

        (await Session(menu)).Digest!.Summary.Samples.Should().Be(1, "the batch of a closed session is dropped");
        (await Session(menu)).Tags.Should().Equal("menu");
        (await Session(run)).Tags.Should().Equal("run", "stage.first");
        (await Session(run)).Status.Should().Be(TelemetrySessionStatus.Ended);
        (await Session(again)).Status.Should().Be(TelemetrySessionStatus.Ended);

        lifetime.Terminate();
    }

    [Fact]
    public async Task ABatchOrAnEndWithTheGuidOfAnotherSocket_ReachesNothing()
    {
        var (owner, ownerLifetime) = Open();
        var (other, otherLifetime) = Open();
        var id = (await owner.Start(Start())).SessionId;
        await other.Start(Start());

        (await other.Accept(OneSample(id))).Enabled.Should().BeFalse();
        await other.End(new SharedClientTelemetry.SessionEnd { SessionId = id });

        (await Reader.GetSamples(id, 0, 10)).Should().BeEmpty();
        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Live);

        ownerLifetime.Terminate();
        otherLifetime.Terminate();
    }

    [Fact]
    public async Task FlagOff_AtTheStart_CreatesNothing()
    {
        var (connection, _) = Open(flag: false);

        var started = await connection.Start(Start("run"));

        started.Enabled.Should().BeFalse();
        started.SessionId.Should().Be(Guid.Empty);
        (await connection.Accept(OneSample(Guid.Empty))).Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task FlagOff_AtABatch_DoesNotWriteIt_AndClosesTheSession()
    {
        var (connection, _) = Open();
        var id = (await connection.Start(Start())).SessionId;
        await connection.Accept(OneSample(id));

        _switch.IsEnabled.Returns(false);

        (await connection.Accept(TelemetryWire.Batch(id, samples: [(2000, [20], false)]))).Enabled.Should().BeFalse();

        (await Reader.GetSamples(id, 0, 10)).Should().HaveCount(1);
        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Ended);
    }

    [Fact]
    public async Task ABadTag_IsDropped_AndCounted_TheStartGoesOn()
    {
        var (connection, _) = Open();
        var id = (await connection.Start(Start("run", "Ivan Petrov"))).SessionId;

        await connection.End(new SharedClientTelemetry.SessionEnd { SessionId = id });

        var session = await Session(id);
        session.Tags.Should().Equal("run");
        session.Digest!.RejectedRequests.Should().Be(1);
    }

    [Fact]
    public async Task TheLimitsAndTheNames_HoldOnTheMessages_AndTheTextsAreScrubbed()
    {
        var (connection, _) = Open();
        var id = (await connection.Start(Start())).SessionId;
        var manySamples = Enumerable.Range(0, ClientTelemetryLimits.MaxSamplesPerBatch + 1).Select(index => ((long)index, new double?[] { 1 }, false)).ToArray();

        (await connection.Accept(TelemetryWire.Batch(id, ["frame.dt", "Ivan Petrov"], manySamples))).Enabled.Should().BeTrue("telemetry over a limit never stops a client");
        (await connection.Accept(TelemetryWire.Batch(id, errors: [TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Error, new string('a', ClientTelemetryLimits.MaxMessageLength + 1))]))).Enabled.Should().BeTrue();
        await connection.Accept(TelemetryWire.Batch(id, errors: [TelemetryWire.Error(2, SharedClientTelemetry.ErrorLevel.Error, "Login failed for ivan.petrov@mail.ru"), TelemetryWire.Error(3, (SharedClientTelemetry.ErrorLevel)9, "a plain log")]));
        await connection.Accept(new SharedClientTelemetry.Batch { SessionId = id, Samples = [null!] });
        await connection.End(new SharedClientTelemetry.SessionEnd { SessionId = id });

        var session = await Session(id);
        session.Digest!.RejectedRequests.Should().Be(3);
        session.Names.Select(name => name.Name).Should().Equal("frame.dt");
        session.NameSlots.Should().Be(2);
        (await Reader.GetSamples(id, 0, 10)).Should().BeEmpty();

        var error = (await Reader.GetErrors(id, 0, 10)).Single();
        error.Message.Should().NotContain("ivan.petrov").And.NotContain("mail.ru");
    }

    [Fact]
    public void ABatchOverTheSize_IsRejected_NaNIsNoValue_AndInfinityBreaksTheBatch()
    {
        var big = TelemetryWire.Batch(Guid.Empty, samples: Enumerable.Range(0, 100).Select(index => ((long)index, Enumerable.Repeat<double?>(1, 100).ToArray(), false)).ToArray());
        TelemetryBatchParser.Parse(big).Rejection.Should().Be(ClientTelemetryRejection.BatchTooLarge);

        var infinity = new SharedClientTelemetry.Batch { Samples = [new SharedClientTelemetry.Sample { Values = [double.PositiveInfinity] }] };
        TelemetryBatchParser.Parse(infinity).Rejection.Should().Be(ClientTelemetryRejection.Malformed);

        TelemetryBatchParser.Parse(TelemetryWire.Batch(Guid.Empty, samples: [(1, [null], true)])).Samples.Single().Should().BeEquivalentTo(new { Values = new double?[] { null }, Hidden = true });
    }

    [Fact]
    public async Task ASecondBatchInFlight_StopsInsteadOfPilingUp()
    {
        var grain = Substitute.For<IClientTelemetrySession>();
        var blocked = new TaskCompletionSource<bool>();
        grain.Accept(Arg.Any<TelemetryBatch>()).Returns(blocked.Task);

        var orleans = Substitute.For<IOrleans>();
        orleans.Grains.GetGrain<IClientTelemetrySession>(Arg.Any<Guid>(), Arg.Any<string?>()).Returns(grain);

        _switch.IsEnabled.Returns(true);
        var connection = new ClientTelemetryConnection(_switch, orleans, NullLogger<ClientTelemetryConnection>.Instance);
        var id = (await connection.Start(Start())).SessionId;

        var first = connection.Accept(OneSample(id));
        (await connection.Accept(OneSample(id))).Enabled.Should().BeFalse();

        blocked.SetResult(true);
        (await first).Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task AFailedCloseAtTheDisconnect_IsLogged_AndTheTimerClosesLater()
    {
        var (connection, _) = Open();
        var id = (await connection.Start(Start())).SessionId;
        await connection.Accept(OneSample(id));

        Cluster.Storage.FailWrites(true);
        await connection.OnDisconnected();
        Cluster.Storage.FailWrites(false);

        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Live);

        Cluster.Clock.Advance(Cluster.Options.LostAfter + TimeSpan.FromSeconds(1));
        await TestWait.Until(async () => (await Reader.GetSession(id))?.Status == TelemetrySessionStatus.Lost);
        (await Session(id)).Digest!.Summary.Samples.Should().Be(1);
    }

    [Fact]
    public async Task AGatewayGoneWithoutTheClose_LeavesItToTheTimer_AsLost()
    {
        var (connection, _) = Open();
        var id = (await connection.Start(Start())).SessionId;
        await connection.Accept(OneSample(id));

        Cluster.Clock.Advance(Cluster.Options.LostAfter + TimeSpan.FromSeconds(1));

        await TestWait.Until(async () => (await Reader.GetSession(id))?.Status == TelemetrySessionStatus.Lost);
        (await connection.Accept(OneSample(id))).Enabled.Should().BeFalse();
    }
}
