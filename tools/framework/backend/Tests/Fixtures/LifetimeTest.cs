using Internal;

namespace Framework.Tests.Fixtures;

// xunit creates and disposes an instance per test, so the subscriptions of a test end with it, failed or not.
public abstract class LifetimeTest : IDisposable
{
    private readonly Lifetime _lifetime = new();

    protected ILifetime TestLifetime => _lifetime;

    public virtual void Dispose()
    {
        _lifetime.Terminate();
    }
}
