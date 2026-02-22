using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public interface IAddressableState<T> : IViewableProperty<T> where T : class, new()
{
    Task SetValue(T value);
}

public class AddressableStateMessageQueueId<T> : IMessageQueueId
{
    public required string Name { get; init; }

    public string ToRaw()
    {
        return $"addressable-state-{Name}";
    }
}

public class AddressableState<T> : ViewableProperty<T>, ILocalSetupCompleted, IAddressableState<T>
    where T : class, new()
{
    public AddressableState(IOrleans orleans, IMessaging messaging) : base(new T())
    {
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    public virtual string Name => typeof(T).FullName!;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await _messaging.ListenQueue<T>(lifetime, new AddressableStateMessageQueueId<T>
            {
                Name = Name
            }, OnUpdate
        );

        var currentValue = await _orleans.GetClusterState<T>(Name);
        Set(currentValue);

        OnSetup(lifetime);
    }

    private void OnUpdate(T value)
    {
        Set(value);
    }

    public Task SetValue(T value)
    {
        Set(value);
        return _orleans.SetClusterState(Name, value);
    }

    protected virtual void OnSetup(IReadOnlyLifetime lifetime)
    {
    }
}

public static class ClusterStateExtensions
{
    public static IHostApplicationBuilder AddClusterState<T>(this IHostApplicationBuilder builder)
        where T : class, new()
    {
        builder.Services.Add<AddressableState<T>>()
            .As<IAddressableState<T>>()
            .As<ILocalSetupCompleted>();

        return builder;
    }

    public static Task SetClusterState<T>(this IOrleans orleans, string name, T value)
    {
        return orleans.Grains.SetClusterState(name, value);
    }

    public static Task SetClusterState<T>(this IGrainFactory grains, string name, T value)
    {
        var grain = grains.GetClusterStateGrain<T>(name);
        return grain.Set(value);
    }

    public static ValueTask<T> GetClusterState<T>(this IOrleans orleans, string name)
    {
        return orleans.Grains.GetClusterState<T>(name);
    }

    extension(IGrainFactory grains)
    {
        public ValueTask<T> GetClusterState<T>(string name)
        {
            var grain = grains.GetClusterStateGrain<T>(name);
            return grain.Get();
        }

        private IAddressableStateStorage<T> GetClusterStateGrain<T>(string name)
        {
            return grains.GetGrain<IAddressableStateStorage<T>>(name);
        }
    }
}