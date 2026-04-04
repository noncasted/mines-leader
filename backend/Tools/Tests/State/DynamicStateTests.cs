using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Tests.State;

/// <summary>
/// Unit tests for DynamicState — ViewableProperty + channel publish on SetValue.
/// Uses mocked IMessaging to avoid full Orleans cluster.
/// </summary>
public class DynamicStateTests {
    [GenerateSerializer]
    public class TestDynamicValue {
        [Id(0)] public string Name { get; set; } = string.Empty;
        [Id(1)] public int Counter { get; set; }

        public override string ToString() => $"Name={Name}, Counter={Counter}";
    }

    private static DynamicState<TestDynamicValue> CreateDynamicState(IMessaging? messaging = null) {
        messaging ??= Substitute.For<IMessaging>();
        var logger = Substitute.For<ILogger<DynamicState<TestDynamicValue>>>();
        return new DynamicState<TestDynamicValue>(messaging, logger, new TestDynamicValue());
    }

    [Fact]
    public void DynamicState_InitialValue_IsDefault() {
        var state = CreateDynamicState();

        state.Value.Should().NotBeNull();
        state.Value.Name.Should().BeEmpty();
        state.Value.Counter.Should().Be(0);
    }

    [Fact]
    public async Task DynamicState_SetValue_UpdatesViewableProperty() {
        var messaging = Substitute.For<IMessaging>();
        var state = CreateDynamicState(messaging);

        var newValue = new TestDynamicValue { Name = "test", Counter = 42 };
        await state.SetValue(newValue);

        state.Value.Should().BeSameAs(newValue);
        state.Value.Name.Should().Be("test");
        state.Value.Counter.Should().Be(42);
    }

    [Fact]
    public async Task DynamicState_SetValue_PublishesToRuntimeChannel() {
        var messaging = Substitute.For<IMessaging>();
        var state = CreateDynamicState(messaging);

        var newValue = new TestDynamicValue { Name = "published", Counter = 7 };
        await state.SetValue(newValue);

        await messaging.RuntimeChannel.Received(1).Publish(
            Arg.Is<IRuntimeChannelId>(id => id.ToRaw().Contains("TestDynamicValue")),
            Arg.Is<TestDynamicValue>(v => v.Name == "published" && v.Counter == 7)
        );
    }

    [Fact]
    public async Task DynamicState_SetValue_SubscriberReceivesUpdate() {
        var messaging = Substitute.For<IMessaging>();
        var state = CreateDynamicState(messaging);

        // Subscribe via View (fires immediately with current + future changes)
        var lifetime = new Lifetime();
        var received = new List<TestDynamicValue>();
        state.View(lifetime, value => received.Add(value));

        // View fires immediately with initial
        received.Should().HaveCount(1);
        received[0].Name.Should().BeEmpty();

        // Set value — subscriber should receive update
        var newValue = new TestDynamicValue { Name = "updated", Counter = 99 };
        await state.SetValue(newValue);

        received.Should().HaveCount(2);
        received[1].Name.Should().Be("updated");
        received[1].Counter.Should().Be(99);

        lifetime.Terminate();
    }

    [Fact]
    public async Task DynamicState_MultipleSetValues_AllPublished() {
        var messaging = Substitute.For<IMessaging>();
        var state = CreateDynamicState(messaging);

        for (var i = 0; i < 5; i++) {
            await state.SetValue(new TestDynamicValue { Name = $"v{i}", Counter = i });
        }

        state.Value.Name.Should().Be("v4");
        state.Value.Counter.Should().Be(4);

        await messaging.RuntimeChannel.Received(5).Publish(
            Arg.Any<IRuntimeChannelId>(),
            Arg.Any<TestDynamicValue>()
        );
    }

    [Fact]
    public async Task DynamicState_SetValue_PublishFailure_DoesNotThrow() {
        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimeChannel.Publish(Arg.Any<IRuntimeChannelId>(), Arg.Any<object>())
            .Returns(Task.FromException(new Exception("Channel down")));

        var state = CreateDynamicState(messaging);

        // Should not throw — error is logged internally
        var act = () => state.SetValue(new TestDynamicValue { Name = "fail" });
        await act.Should().NotThrowAsync();

        // Value is still updated despite publish failure (Set is called before PublishChannel)
        state.Value.Name.Should().Be("fail");
    }
}
