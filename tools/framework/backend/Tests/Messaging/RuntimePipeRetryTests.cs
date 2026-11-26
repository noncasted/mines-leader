using Internal;
using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Messaging;

// Transport-style failures may retry, handler failures do not.
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimePipeRetryTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Send_HandlerFails_DoesNotRetryApplicationException()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = TestLifetime.Child();
        var callCount = 0;

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, _ => {
            Interlocked.Increment(ref callCount);
            return Task.FromException<TestResponse>(new InvalidOperationException("non-idempotent handler failure"));
        });

        var act = () => messaging.SendPipe<TestResponse>(pipeId,
            new TestRequest { Question = "do-not-retry" });

        await act.Should().ThrowAsync<Exception>()
                 .WithMessage("*non-idempotent handler failure*");
        callCount.Should().Be(1);
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_NoHandler_AllRetriesExhausted_Throws()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        var act = () => messaging.SendPipe<TestResponse>(pipeId,
            new TestRequest { Question = "no-handler" });

        await act.Should().ThrowAsync<Exception>();
    }
}
