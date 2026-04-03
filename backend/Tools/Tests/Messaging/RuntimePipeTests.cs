using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests RuntimePipe: request-response pattern, handler binding, error propagation.
/// RuntimePipe is in-memory (no side effects needed).
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimePipeTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture) {
    [Fact]
    public async Task Send_WithHandler_ReturnsResponse() {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(
            lifetime, pipeId,
            req => Task.FromResult(new TestResponse { Answer = $"reply-to-{req.Question}" })
        );

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "hello" });

        response.Answer.Should().Be("reply-to-hello");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_MultipleRequests_EachGetsCorrectResponse() {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(
            lifetime, pipeId,
            req => Task.FromResult(new TestResponse { Answer = req.Question.ToUpper() })
        );

        var r1 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "one" });
        var r2 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "two" });
        var r3 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "three" });

        r1.Answer.Should().Be("ONE");
        r2.Answer.Should().Be("TWO");
        r3.Answer.Should().Be("THREE");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_NoHandler_ThrowsException() {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        var act = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "nobody-home" });

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Send_HandlerThrows_PropagatesException() {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(
            lifetime, pipeId,
            _ => throw new InvalidOperationException("handler-error")
        );

        var act = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "boom" });

        await act.Should().ThrowAsync<Exception>().WithMessage("*handler-error*");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_AsyncHandler_AwaitsCorrectly() {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(
            lifetime, pipeId,
            async req => {
                await Task.Delay(50);
                return new TestResponse { Answer = $"delayed-{req.Question}" };
            }
        );

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "wait" });

        response.Answer.Should().Be("delayed-wait");
        lifetime.Terminate();
    }
}
