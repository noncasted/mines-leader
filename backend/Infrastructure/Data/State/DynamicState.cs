using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IDynamicState<T> : IViewableProperty<T> where T : class, new()
{
    Task SetValue(T value);
}

public class DynamicStateChannelId<T> : IRuntimeChannelId
{
    public string ToRaw()
    {
        var type = typeof(T);
        return $"dynamic-state-{type.FullName!}";
    }
}

public class DynamicState<T> : ViewableProperty<T>, ILocalSetupCompleted, IDynamicState<T> where T : class, new()
{
    public DynamicState(IMessaging messaging, ILogger<DynamicState<T>> logger, T baseValue) : base(baseValue)
    {
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IMessaging _messaging;
    private readonly ILogger<DynamicState<T>> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        OnInitialized(lifetime);

        try
        {
            await _messaging.ListenChannel<T>(lifetime, new DynamicStateChannelId<T>(), OnUpdate);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DynamicState] Failed to listen channel for {Type}", typeof(T).Name);
        }
    }

    public async Task SetValue(T value)
    {
        await _writeLock.WaitAsync();

        try
        {
            Set(value);
            _logger.LogInformation("[DynamicState] Set {Type} : {Value}", typeof(T).Name, value.ToString());
            await _messaging.PublishChannel(new DynamicStateChannelId<T>(), value);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DynamicState] Failed to publish {Type}: {Value}", typeof(T).Name, value);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    protected virtual void OnInitialized(IReadOnlyLifetime lifetime)
    {
    }

    private void OnUpdate(T value)
    {
        Set(value);
        _logger.LogInformation("[DynamicState] Received {Type} : {Value}", typeof(T).Name, value.ToString());
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