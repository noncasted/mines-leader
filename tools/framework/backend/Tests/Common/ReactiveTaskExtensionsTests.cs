using FluentAssertions;
using Framework.Tests.Fixtures;
using Internal;
using Xunit;

namespace Framework.Tests.Common;

// The Task waits of the backend over the reactive primitives; the primitives themselves are tested in common/Tests~.
public class ReactiveTaskExtensionsTests : LifetimeTest
{
    [Fact]
    public async Task WaitTrue_CompletesOnTrue()
    {
        var property = new ViewableProperty<bool>(false);

        var wait = property.WaitTrue(new Lifetime(TestLifetime));
        wait.IsCompleted.Should().BeFalse();

        property.Set(true);

        await wait;
    }

    [Fact]
    public void WaitTrue_AlreadyTrue_IsCompleted()
    {
        var property = new ViewableProperty<bool>(true);

        property.WaitTrue(new Lifetime(TestLifetime)).IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WaitTrue_LifetimeEnds_IsCancelled()
    {
        var property = new ViewableProperty<bool>(false);
        var lifetime = new Lifetime(TestLifetime);

        var wait = property.WaitTrue(lifetime);
        lifetime.Terminate();

        await FluentActions.Awaiting(() => wait).Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task WaitInvoke_CompletesWithTheValue()
    {
        var source = new ViewableDelegate<int>();

        var wait = source.WaitInvoke(new Lifetime(TestLifetime));
        source.Invoke(42);

        (await wait).Should().Be(42);
    }

    [Fact]
    public async Task WaitInvoke_LifetimeEnds_IsCancelled()
    {
        var source = new ViewableDelegate();
        var lifetime = new Lifetime(TestLifetime);

        var wait = source.WaitInvoke(lifetime);
        lifetime.Terminate();

        await FluentActions.Awaiting(() => wait).Should().ThrowAsync<TaskCanceledException>();
    }
}
