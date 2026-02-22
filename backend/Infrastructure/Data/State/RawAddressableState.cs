using System.Text.Json;
using Common.Reactive;
using Infrastructure;

namespace Cluster.Configs;

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
                Value = JsonSerializer.Deserialize<T>(raw.Raw)!;
            }
        );
    }

    public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
    {
        Advise(lifetime, (valueLifetime, raw) =>
            {
                var value = JsonSerializer.Deserialize<T>(raw.Raw)!;
                handler(valueLifetime, value);
            }
        );
    }

    public Task SetValue(T value)
    {
        return SetValue(new RawAddressableState()
            {
                Raw = JsonSerializer.Serialize(value),
            }
        );
    }
}