using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Messaging;

[Collection(nameof(SideEffectIntegrationCollection))]
public class DurableQueueTests(SideEffectTestFixture fixture) : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    [Fact]
    public async Task PushDirect_SingleMessage_DeliveredToListener()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime,
            queueId,
            msg => received.TrySetResult(msg));

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "hello", Sequence = 1 });
        await DrainSideEffects();

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.Text.Should().Be("hello");
        result.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task PushDirect_MultipleMessages_AllDelivered()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime,
            queueId,
            msg => {
                lock (received)
                {
                    received.Add(msg);

                    if (received.Count >= 5)
                        allReceived.TrySetResult();
                }
            });

        for (var i = 0; i < 5; i++)
            await messaging.PushDirectQueue(queueId, new TestMessage { Text = $"msg-{i}", Sequence = i });

        await DrainSideEffects();
        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        received.Should().HaveCount(5);
        received.Select(m => m.Sequence).Should().BeEquivalentTo([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task PushDirect_MultipleListeners_AllReceive()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received1 = new TaskCompletionSource<TestMessage>();
        var received2 = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime, queueId, msg => received1.TrySetResult(msg));
        await messaging.ListenDurableQueue<TestMessage>(TestLifetime, queueId, msg => received2.TrySetResult(msg));

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "broadcast", Sequence = 42 });
        await DrainSideEffects();

        var r1 = await received1.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var r2 = await received2.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        r1.Text.Should().Be("broadcast");
        r2.Text.Should().Be("broadcast");
    }

    [Fact]
    public async Task PushDirect_ListenerTerminated_NoDelivery()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var lifetime = TestLifetime.Child();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(lifetime, queueId, msg => {
            lock (received)
                received.Add(msg);
        });

        // Terminate the listener
        lifetime.Terminate();

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "ghost" });
        var drain = await DrainSideEffectsWithFailures();

        // The side effect delivers to the listeners while it runs, so nothing can come after the drain.
        drain.AllSucceeded.Should().BeFalse("nobody is left to acknowledge the message");
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task PushDirect_TerminatedListener_DoesNotAckSideEffect()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var lifetime = TestLifetime.Child();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(lifetime, queueId, _ => {
        });

        lifetime.Terminate();

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "must-not-ack" });
        var drain = await DrainSideEffectsWithFailures();

        drain.AllSucceeded.Should().BeFalse();
        drain.ExecutionTrace.Should().Contain(e =>
            e.Success == false &&
            e.ErrorMessage != null &&
            (e.ErrorMessage.Contains("No active subscribers") ||
             e.ErrorMessage.Contains("No subscribers successfully processed")));
    }

    [Fact]
    public async Task PushDirect_AsyncListener_AwaitsBeforeSideEffectCompletes()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime, queueId, async _ => {
            await Task.Delay(50);
            completed.TrySetResult();
        });

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "async" });
        var drain = await DrainSideEffects();

        drain.AssertDrainedWithWork();
        completed.Task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task PushTransactional_WithinTransaction_DeliveredAfterCommit()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime, queueId, msg => received.TrySetResult(msg));

        await RunTransaction(() => {
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-msg", Sequence = 99 });
            return Task.CompletedTask;
        });

        await DrainSideEffects();

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.Text.Should().Be("tx-msg");
        result.Sequence.Should().Be(99);
    }

    [Fact]
    public async Task PushTransactional_TransactionRollback_NotDelivered()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime, queueId, msg => {
            lock (received)
                received.Add(msg);
        });

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(() => {
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "should-not-arrive" });
            return Task.FromException(new Exception("Intentional rollback"));
        });

        result.IsSuccess.Should().BeFalse();

        // The drain is the signal: a transactional push is delivered only through a side effect, and none was written.
        var drain = await Pipeline!.DrainUntilQuiet();
        drain.TotalTasks.Should().Be(0);

        lock (received)
            received.Should().BeEmpty();
    }

    [Fact]
    public async Task PushTransactional_MultipleMessagesInTransaction_AllDelivered()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime, queueId, msg => {
            lock (received)
            {
                received.Add(msg);

                if (received.Count >= 3)
                    allReceived.TrySetResult();
            }
        });

        await RunTransaction(() => {
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-0", Sequence = 0 });
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-1", Sequence = 1 });
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-2", Sequence = 2 });
            return Task.CompletedTask;
        });

        await DrainSideEffects();
        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        received.Should().HaveCount(3);
        received.Select(m => m.Sequence).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact]
    public async Task PushDirect_NoListeners_DoesNotBufferMessages()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        // Push to a queue nobody is listening to
        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "old" });
        var drain = await DrainSideEffectsWithFailures();
        drain.AllSucceeded.Should().BeFalse("nobody acknowledges a message pushed with no listeners");

        // Subscribe after the push: the drain delivers the new message while it runs, the old one would come first.
        var received = new List<TestMessage>();

        await messaging.ListenDurableQueue<TestMessage>(TestLifetime, queueId, msg => {
            lock (received)
                received.Add(msg);
        });

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "new", Sequence = 1 });
        await DrainSideEffects();

        lock (received)
            received.Select(message => message.Text).Should().Equal("new");
    }
}