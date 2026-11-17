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

// A SideEffectsWorker of its own over the database of the test cluster (the fixtures have no worker): an effect
// running longer than the stuck threshold, the shutdown while the effects are disabled, a payload that does not
// deserialize.
[Collection(nameof(OrleansIntegrationCollection))]
public class SideEffectsWorkerTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task LongRunningEffect_NotRequeuedAsStuck()
    {
        var trackingId = Guid.NewGuid();

        // Stuck after 0.5 s, checked on every scan; the effect runs longer. The threshold is above the time between
        // the touch and the check of one scan on a loaded machine.
        var worker = CreateWorker(new SideEffectsOptions
        {
            ScanDelay = 50,
            EmptyScanDelay = 50,
            StuckCheckIntervalSeconds = 0,
            StuckThresholdMinutes = 0.5f / 60
        }, enabled: true);

        await GetSiloService<ISideEffectsStorage>().Write(new SlowSideEffect { TrackingId = trackingId });
        await worker.StartAsync(TestContext.Current.CancellationToken);

        await TestWait.Until(() => SlowSideEffect.Finished.ContainsKey(trackingId));
        await worker.StopAsync(TestContext.Current.CancellationToken);

        SlowSideEffect.Executions.GetValueOrDefault(trackingId).Should()
                      .Be(1, "a running effect is not stuck: running it again in parallel duplicates it");
    }

    [Fact]
    public async Task Stop_WhileDisabled_LoopEnds()
    {
        var flags = Substitute.For<IClusterFlags>();
        var checks = 0;

        flags.SideEffectsEnabled.Returns(_ => {
            Interlocked.Increment(ref checks);
            return false;
        });

        var worker = CreateWorker(new SideEffectsOptions(), flags);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.Until(() => Volatile.Read(ref checks) >= 1);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        // A window longer than the 500 ms between the checks of a running loop that sees the effects disabled: the loop
        // has no signal of its end but the absence of checks after it.
        var afterStop = Volatile.Read(ref checks);
        await Task.Delay(700, TestContext.Current.CancellationToken);

        Volatile.Read(ref checks).Should().Be(afterStop, "the loop of a stopped worker must end");
    }

    [Fact]
    public async Task UnknownPayloadType_DeadLetterKeepsOriginalError()
    {
        var type = $"Missing.Effect{Guid.NewGuid():N}";

        await using (var connection = await Database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                                  INSERT INTO side_effects_queue (id, payload, retry_count, created_at)
                                  VALUES (@id, jsonb_build_object('$type', @type || ', Missing.Assembly'), 0, now())
                                  """;
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("type", type);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        // One attempt: the first failure goes to the dead letter.
        var worker = CreateWorker(new SideEffectsOptions
        {
            ScanDelay = 50,
            EmptyScanDelay = 50,
            MaxRetryCount = 1
        }, enabled: true);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        var error = await SideEffectRows.WaitDeadLetterError(Database.DataSource, type);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        error.Should().Contain(type, "the dead letter must keep the deserialization error");
    }

    private SideEffectsWorker CreateWorker(SideEffectsOptions options, bool enabled)
    {
        var flags = Substitute.For<IClusterFlags>();
        flags.SideEffectsEnabled.Returns(enabled);
        return CreateWorker(options, flags);
    }

    private SideEffectsWorker CreateWorker(SideEffectsOptions options, IClusterFlags flags)
    {
        var config = Substitute.For<ISideEffectsConfig>();
        config.Value.Returns(options);

        var loopObserver = Substitute.For<IServiceLoopObserver>();
        loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));

        var participant = Substitute.For<IClusterParticipantContext>();
        participant.IsInitialized.Returns(new SharedViewableProperty<bool>(true));

        return new SideEffectsWorker(
            GetSiloService<ISideEffectsStorage>(),
            GetSiloService<ITransactions>(),
            GetSiloService<IOrleans>(),
            loopObserver,
            config,
            flags,
            participant,
            NullLogger<SideEffectsWorker>.Instance);
    }
}
