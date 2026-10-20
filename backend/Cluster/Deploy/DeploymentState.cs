using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public interface IDeploymentState<T> : IViewableProperty<T> where T : class, new()
{
    bool IsInitialized { get; }
    Task SetValue(T value);
}

public class DeploymentStateChannelId<T> : IRuntimeChannelId
{
    public DeploymentStateChannelId(Guid deployId)
    {
        _deployId = deployId;
    }

    private readonly Guid _deployId;

    public string ToRaw()
    {
        var type = typeof(T);
        return $"deployment-state-{type.FullName}-{_deployId:N}";
    }
}

public class DeploymentState<T> : ViewableProperty<T>, IDeploymentState<T>, IDeployAware where T : class, new()
{
    public DeploymentState(
        IOrleans orleans,
        IMessaging messaging,
        ILoggerFactory loggerFactory,
        T baseValue) : base(baseValue)
    {
        _orleans = orleans;
        _messaging = messaging;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    private readonly IMessaging _messaging;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Values are applied from the loading epoch, own writes and channel deliveries, on different threads.
    // Set is not thread-safe (the listeners and value lifetimes are plain lists): concurrent Sets threw
    // "Collection was modified" and the watermark check-then-set let an older value overwrite a newer one.
    // Taken only around the synchronous apply (watermark + Set + listeners), never across an await.
    private readonly object _applyLock = new();
    private StateIdentity? _identity;
    private DeploymentStateChannelId<T>? _channelId;
    private volatile bool _isInitialized;

    // UpdateDate of the last state applied to Value (from load, SetValue, or a channel
    // update). Channel updates include our own publishes echoed back with messaging
    // latency — without this watermark an old echo rolls Value back and the next
    // read-modify-write (ClusterFeatures.Apply) publishes a state computed from the
    // stale base, clobbering newer flags cluster-wide.
    private DateTime _lastAppliedUpdate;

    protected readonly IOrleans _orleans;
    protected readonly ILogger _logger;

    protected Guid _deployId;

    public bool IsInitialized => _isInitialized;

    protected virtual StateIdentity CreateStateIdentity(Guid deployId)
    {
        return CreateDefaultStateIdentity(deployId);
    }

    public static StateIdentity CreateDefaultStateIdentity(Guid deployId)
    {
        return new StateIdentity
        {
            Key = deployId,
            Type = $"deployment_state_{typeof(T).Name}",
            TableName = "cluster",
            Extension = null
        };
    }

    public virtual async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        // The value of the previous epoch is not valid for the new one: readers must not see
        // IsInitialized == true until the new epoch is loaded. A load failure propagates to
        // DeployContext, which ends the epoch and crashes the caller.
        _isInitialized = false;

        _deployId = newDeployId;

        _identity = CreateStateIdentity(newDeployId);
        _channelId = new DeploymentStateChannelId<T>(newDeployId);

        var state = await _orleans.StateStorage.Read<AddressableStateValue>(_identity);

        var value = state.IsInitialized ? _orleans.Serializer.Deserialize<T>(state.Value) : new T();

        lock (_applyLock)
        {
            _lastAppliedUpdate = state.IsInitialized ? state.UpdateDate : default;
            Set(value);
        }

        await _messaging.ListenChannel<AddressableStateValue>(deployLifetime, _channelId, OnUpdate);
        _isInitialized = true;
    }

    public virtual async Task SetValue(T value)
    {
        await _writeLock.WaitAsync();

        try
        {
            if (_deployId == Guid.Empty || _identity == null || _channelId == null)
                throw new InvalidOperationException("DeploymentState is not initialized with a valid deploy ID.");

            var state = new AddressableStateValue
            {
                IsInitialized = true,
                UpdateDate = DateTime.UtcNow,
                Value = _orleans.Serializer.Serialize(value)
            };

            lock (_applyLock)
            {
                _lastAppliedUpdate = state.UpdateDate;
                Set(value);
            }

            await _orleans.StateStorage.Write(_identity, state);
            await _messaging.PublishChannel(_channelId, state);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DeploymentState] Failed to persist {Type}: {Value}", typeof(T).Name, value);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    protected virtual void OnUpdate(AddressableStateValue state)
    {
        TryApplyUpdate(state);
    }

    protected bool TryApplyUpdate(AddressableStateValue state)
    {
        if (state.IsInitialized == false)
            return false;

        lock (_applyLock)
        {
            if (state.UpdateDate <= _lastAppliedUpdate)
            {
                _logger.LogDebug(
                    "[DeploymentState] Skipped stale update for {Type}: sent {Sent:O}, last applied {Applied:O}",
                    typeof(T).Name, state.UpdateDate, _lastAppliedUpdate);
                return false;
            }

            // A value that does not deserialize is a schema mismatch between services: fail the delivery loudly
            // instead of keeping a silently stale value.
            T value;

            try
            {
                value = _orleans.Serializer.Deserialize<T>(state.Value);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"[DeploymentState] Failed to deserialize update for {typeof(T).Name} (deploy {_deployId})", e);
            }

            _lastAppliedUpdate = state.UpdateDate;
            Set(value);
            return true;
        }
    }
}

public static class DeploymentStateExtensions
{
    public static IHostApplicationBuilder AddDeploymentState<T>(this IHostApplicationBuilder builder)
        where T : class, new()
    {
        builder.Services.AddSingleton(new T());

        builder.Add<DeploymentState<T>>()
               .As<IDeploymentState<T>>()
               .As<IDeployAware>();

        return builder;
    }
}

[Obsolete("Use IDeploymentState<T> instead.")]
public interface ILiveState<T> : IViewableProperty<T> where T : class, new()
{
    Task SetValue(T value);
}

[Obsolete("Use DeploymentState<T> instead.")]
public class LiveState<T> : DeploymentState<T>, ILiveState<T> where T : class, new()
{
    public LiveState(IOrleans orleans, IMessaging messaging, ILoggerFactory loggerFactory, T baseValue)
        : base(orleans, messaging, loggerFactory, baseValue)
    {
    }
}

public static class LiveStateExtensions
{
    [Obsolete("Use DeploymentStateExtensions.AddDeploymentState instead.")]
    public static IHostApplicationBuilder AddLiveState<T>(this IHostApplicationBuilder builder)
        where T : class, new()
    {
        return builder.AddDeploymentState<T>();
    }
}

public class LiveStateChannelId<T> : IRuntimeChannelId
{
    public LiveStateChannelId(Guid deployId)
    {
        _deployId = deployId;
    }

    private readonly Guid _deployId;

    public string ToRaw()
    {
        var type = typeof(T);
        return $"live-state-{type.FullName}-{_deployId:N}";
    }
}

public static class DeploymentStateCompat
{
    public static IRuntimeChannelId ToChannelId<T>(this Guid deployId) where T : class, new()
    {
        return new DeploymentStateChannelId<T>(deployId);
    }

    public static StateIdentity ToDeploymentStateIdentity<T>(this Guid deployId) where T : class, new()
    {
        return new StateIdentity
        {
            Key = $"{deployId:N}:{typeof(T).Name}",
            Type = "deployment_state",
            TableName = "cluster",
            Extension = null
        };
    }

    public static async Task<T?> ReadDeploymentState<T>(this IOrleans orleans, Guid deployId) where T : class, new()
    {
        var identity = deployId.ToDeploymentStateIdentity<T>();
        var state = await orleans.StateStorage.Read<AddressableStateValue>(identity);

        if (state.IsInitialized == false)
            return null;

        return orleans.Serializer.Deserialize<T>(state.Value);
    }

    public static async Task WriteDeploymentState<T>(this IOrleans orleans, Guid deployId, T value)
        where T : class, new()
    {
        var identity = deployId.ToDeploymentStateIdentity<T>();

        var state = new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = DateTime.UtcNow,
            Value = orleans.Serializer.Serialize(value)
        };
        await orleans.StateStorage.Write(identity, state);
    }

    public static Task PublishDeploymentState<T>(this IMessaging messaging, Guid deployId, T value, IOrleans orleans)
        where T : class, new()
    {
        var channelId = new DeploymentStateChannelId<T>(deployId);

        var state = new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = DateTime.UtcNow,
            Value = orleans.Serializer.Serialize(value)
        };
        return messaging.PublishChannel(channelId, state);
    }
}