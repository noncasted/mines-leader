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

// The dead letter of a failed transactional effect keeps its cause; the entries a worker took before its stop are
// executed, not left in processing until the stuck check of another worker.
[Collection(nameof(OrleansIntegrationCollection))]
public class SideEffectsWorkerFailureTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task TransactionalEffectFails_DeadLetterKeepsCause()
    {
        var marker = $"cause-{Guid.NewGuid():N}";
        var storage = GetSiloService<ISideEffectsStorage>();
        await storage.Write(new FailingTransactionalSideEffect { Marker = marker });

        var worker = CreateWorker(storage, new SideEffectsOptions
        {
            ScanDelay = 50,
            EmptyScanDelay = 50,
            MaxRetryCount = 1
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);
        var error = await SideEffectRows.WaitDeadLetterError(Database.DataSource, marker);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        error.Should().Contain(marker, "the dead letter must keep the error of the effect");
    }

    [Fact]
    public async Task Stop_WhileReading_TakenEntriesExecuted()
    {
        var storage = Substitute.For<ISideEffectsStorage>();
        var read = new TaskCompletionSource<IReadOnlyList<SideEffectEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;

        storage.Read(Arg.Any<int>()).Returns(_ => Interlocked.Increment(ref reads) == 1
            ? read.Task
            : Task.FromResult<IReadOnlyList<SideEffectEntry>>(Array.Empty<SideEffectEntry>()));

        var trackingId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var worker = CreateWorker(storage, new SideEffectsOptions { ScanDelay = 50, EmptyScanDelay = 50 });
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await TestWait.Until(() => Volatile.Read(ref reads) >= 1);

        var stop = worker.StopAsync(TestContext.Current.CancellationToken);

        // The entries are already moved to processing by the read: nobody else takes them before the stuck threshold.
        read.SetResult(new[]
        {
            new SideEffectEntry { Id = entryId, Effect = new RecordingSideEffect { TrackingId = trackingId }, RetryCount = 0 }
        });

        await stop;

        RecordingSideEffect.Executions.ContainsKey(trackingId).Should().BeTrue("a taken entry must not hang in processing");
        await storage.Received(1).CompleteProcessing(entryId);
    }

    // Every slot busy: the worker still touches its entries at its stuck check, otherwise the stuck check of another
    // worker takes them for the entries of a gone worker and runs them again.
    [Fact]
    public async Task SlotsFull_RunningEntriesTouched()
    {
        var storage = Substitute.For<ISideEffectsStorage>();
        var gate = TestGate.Create();
        var entryId = Guid.NewGuid();
        var reads = 0;
        var touches = 0;

        storage.Read(Arg.Any<int>()).Returns(_ => Task.FromResult<IReadOnlyList<SideEffectEntry>>(
            Interlocked.Increment(ref reads) == 1
                ? new[] { new SideEffectEntry { Id = entryId, Effect = new GatedSideEffect { GateId = gate.Id }, RetryCount = 0 } }
                : Array.Empty<SideEffectEntry>()));

        storage.TouchProcessing(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(entryId)))
               .Returns(_ => {
                   Interlocked.Increment(ref touches);
                   return Task.CompletedTask;
               });

        var worker = CreateWorker(storage, new SideEffectsOptions
        {
            ScanDelay = 50,
            EmptyScanDelay = 50,
            ConcurrentExecutions = 1,
            StuckCheckIntervalSeconds = 0
        });

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await gate.Entered;
        await TestWait.Until(() => Volatile.Read(ref touches) >= 1);

        gate.Release();
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    private SideEffectsWorker CreateWorker(ISideEffectsStorage storage, SideEffectsOptions options)
    {
        var flags = Substitute.For<IClusterFlags>();
        flags.SideEffectsEnabled.Returns(true);

        var config = Substitute.For<ISideEffectsConfig>();
        config.Value.Returns(options);

        var loopObserver = Substitute.For<IServiceLoopObserver>();
        loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));

        var participant = Substitute.For<IClusterParticipantContext>();
        participant.IsInitialized.Returns(new SharedViewableProperty<bool>(true));

        return new SideEffectsWorker(
            storage,
            GetSiloService<ITransactions>(),
            GetSiloService<IOrleans>(),
            loopObserver,
            config,
            flags,
            participant,
            NullLogger<SideEffectsWorker>.Instance);
    }
}
