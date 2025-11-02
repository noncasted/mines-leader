using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDynamicState<T> : IViewableProperty<T> where T : class, new()
{
    Task SetValue(T value);
}

public class DynamicStateMessageQueueId<T> : IMessageQueueId
{
    public string ToRaw()
    {
        var type = typeof(T);
        return $"dynamic-state-{type.FullName!}";
    }
}

public class DynamicState<T> : ViewableProperty<T>, ILocalSetupCompleted, IDynamicState<T> where T : class, new()
{
    public DynamicState(IMessaging messaging, ILogger logger, T baseValue) : base(baseValue)
    {
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly ILogger _logger;

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        OnSetup(lifetime);
        return _messaging.ListenQueue<T>(lifetime, new DynamicStateMessageQueueId<T>(), OnUpdate);
    }

    private void OnUpdate(T value)
    {
        Set(value);
        _logger.LogInformation("[Cluster] [DynamicState] Update {Type} : {Key}", typeof(T).Name, value.ToString());
    }

    public Task SetValue(T value)
    {
        Set(value);
        _logger.LogInformation("[Cluster] [DynamicState] Set {Type} : {Key}", typeof(T).Name, value.ToString());
        return _messaging.PushDirectQueue(new DynamicStateMessageQueueId<T>(), value);
    }

    protected virtual void OnSetup(IReadOnlyLifetime lifetime)
    {
    }
}

public static class DynamicStateExtensions
{
    public static IHostApplicationBuilder AddDynamicState<T>(this IHostApplicationBuilder builder)
        where T : class, new()
    {
        builder.Services.Add<DynamicState<T>>()
            .As<IDynamicState<T>>()
            .As<ILocalSetupCompleted>();

        return builder;
    }
}