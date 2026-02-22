using Common.Extensions;
using Common.Reactive;

namespace Infrastructure;

[GenerateSerializer]
public class RawAddressableState
{
    [Id(0)]
    public string Raw { get; set; }
}

public abstract class RawAddressableStateView<T> :
    AddressableState<RawAddressableState>,
    IAddressableState<T> where T : class, new()
{
    public RawAddressableStateView(IOrleans orleans, IMessaging messaging) : base(orleans, messaging)
    {
        Value = new T();
    }

    public new T Value { get; private set; }

    protected override void OnSetup(IReadOnlyLifetime lifetime)
    {
        this!.ViewNotNull<RawAddressableState>(lifetime, raw =>
            {
                try
                {
                    var newValue = JsonUtils.Deserialize<T>(raw.Raw)!;
                    Value = newValue;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        );
    }

    public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
    {
        Advise(lifetime, (valueLifetime, raw) =>
            {
                var value = JsonUtils.Deserialize<T>(raw.Raw)!;
                handler(valueLifetime, value);
            }
        );
    }

    public Task SetValue(T value)
    {
        return SetValue(new RawAddressableState()
            {
                Raw = JsonUtils.Serialize(value),
            }
        );
    }
}