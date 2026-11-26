using Internal;
using Infrastructure;

namespace Framework.Tests.Fixtures;

public class TestAddressableState<T> : ViewableProperty<T>, IAddressableState<T>
    where T : class, new()
{
    public TestAddressableState() : base(new T())
    {
    }

    public TestAddressableState(T value) : base(value)
    {
    }

    public bool IsInitialized => true;

    public Task SetValue(T value)
    {
        Set(value);
        return Task.CompletedTask;
    }
}