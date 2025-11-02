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
    public string ToRaw()
    {
        var type = typeof(T);
        return $"addressable-state-{type.FullName!}";
    }
}

public class AddressableState<T> : ViewableProperty<T>, ILocalSetupCompleted, IAddressableState<T> where T : class, new()
{
    public AddressableState(IOrleans orleans, IMessaging messaging) : base(new T())
    {
        _orleans = orleans;
        _messaging = messaging;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await _messaging.ListenQueue<T>(lifetime, new AddressableStateMessageQueueId<T>(), OnUpdate);

        var currentValue = await _orleans.GetClusterState<T>();
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
        return _orleans.SetClusterState(value);
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

    public static Task SetClusterState<T>(this IOrleans orleans, T value)
    {
        return orleans.Grains.SetClusterState(value);
    }

    public static Task SetClusterState<T>(this IGrainFactory grains, T value)
    {
        var grain = grains.GetClusterStateGrain<T>();
        return grain.Set(value);
    }

    public static ValueTask<T> GetClusterState<T>(this IOrleans orleans)
    {
        return orleans.Grains.GetClusterState<T>();
    }

    extension(IGrainFactory grains)
    {
        public ValueTask<T> GetClusterState<T>()
        {
            var grain = grains.GetClusterStateGrain<T>();
            return grain.Get();
        }

        private IAddressableStateStorage<T> GetClusterStateGrain<T>()
        {
            var type = typeof(T);
            var grainId = type.FullName!;
            return grains.GetGrain<IAddressableStateStorage<T>>(grainId);
        }
    }
}