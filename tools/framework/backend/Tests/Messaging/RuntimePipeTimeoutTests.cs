using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Xunit;

namespace Framework.Tests.Messaging;

// A slow handler is not a dead observer: a timed-out request is not sent again (the handler may have run) and the
// observer stays bound for the next requests.
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimePipeTimeoutTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Send_HandlerTimesOut_NotRetriedAndObserverKept()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = TestLifetime.Child();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeout = OverrideConfig<IRuntimePipeConfig, RuntimePipeOptions>(new RuntimePipeOptions
        {
            SendTimeoutSeconds = 1
        });

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, async request => {
            if (Interlocked.Increment(ref calls) == 1)
                await release.Task;

            return new TestResponse { Answer = request.Question };
        });

        var slow = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "slow" });
        await slow.Should().ThrowAsync<TimeoutException>();

        // The slow call finishes in the observer, the next one waits behind it.
        release.SetResult();

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "next" });

        response.Answer.Should().Be("next");
        calls.Should().Be(2, "every request runs the handler once");
        lifetime.Terminate();
    }

    // The request that timed out fails later in the handler: nobody awaits it, but its failure must be observed, an
    // unobserved one reaches TaskScheduler.UnobservedTaskException.
    [Fact]
    public async Task Send_TimedOutRequestFailsLater_FailureObserved()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = TestLifetime.Child();
        var marker = $"late-{Guid.NewGuid():N}";
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unobserved = 0;

        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            if (args.Exception.ToString().Contains(marker))
                Interlocked.Increment(ref unobserved);
        }

        using var timeout = OverrideConfig<IRuntimePipeConfig, RuntimePipeOptions>(new RuntimePipeOptions
        {
            SendTimeoutSeconds = 1
        });

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, async request => {
            if (request.Question == "late")
            {
                await release.Task;
                throw new InvalidOperationException(marker);
            }

            return new TestResponse { Answer = request.Question };
        });

        TaskScheduler.UnobservedTaskException += OnUnobserved;

        try
        {
            var late = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "late" });
            await late.Should().ThrowAsync<TimeoutException>();
            release.SetResult();

            // The observer runs one request at a time: the answer of the next one comes after the late failure.
            (await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "next" })).Answer.Should().Be("next");

            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Volatile.Read(ref unobserved).Should().Be(0, "the late failure of a timed out request must be observed");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
            lifetime.Terminate();
        }
    }

    [Fact]
    public async Task Exists_ObserverBusy_ObserverKept()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = TestLifetime.Child();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The busy request outlasts the liveness check whatever the load, but must not time out itself.
        using var timeout = OverrideConfig<IRuntimePipeConfig, RuntimePipeOptions>(new RuntimePipeOptions
        {
            SendTimeoutSeconds = 30
        });

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, async request => {
            if (request.Question == "busy")
            {
                entered.SetResult();
                await release.Task;
            }

            return new TestResponse { Answer = request.Question };
        });

        // The liveness ping waits behind the busy request of the observer.
        var busy = messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "busy" });
        await entered.Task;
        (await messaging.IsPipeExists(pipeId)).Should().BeFalse("the ping of a busy observer does not answer in time");
        release.SetResult();
        await busy;

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "next" });

        response.Answer.Should().Be("next");
        lifetime.Terminate();
    }
}
