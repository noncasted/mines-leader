using Common.Extensions;
using Internal;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public interface IDeploymentState<T> : ISharedViewableProperty<T> where T : class, new()
{
    bool IsInitialized { get; }
    Task SetValue(T value);
}

public class DeploymentState<T> : SharedViewableProperty<T>, IDeploymentState<T>, IDeployAware where T : class, new()
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

    // Values are applied from the loading epoch, own writes and channel deliveries, on different threads, and the pages of
    // the console subscribe on theirs: _gate of SharedViewableProperty is taken around the synchronous apply (epoch check +
    // watermark + Set + listeners), never across an await. Without it the watermark check-then-set let an older value
    // overwrite a newer one.
    //
    // The deploy epoch the value belongs to, replaced under _gate by OnDeployChanged. A write or a read started
    // in one epoch applies its result only while that epoch is still the current one.
    private Epoch? _epoch;
    private volatile bool _isInitialized;

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

        var epoch = new Epoch(newDeployId, CreateStateIdentity(newDeployId), new DeploymentStateChannelId<T>(newDeployId));

        lock (_gate)
        {
            _epoch = epoch;
            _deployId = newDeployId;
        }

        // Listening before the read: an update published while the read runs is not lost, the watermark keeps the
        // newer of the two. A gap of the channel (missed updates) reloads the value from the database.
        await _messaging.ListenChannel<AddressableStateValue>(deployLifetime, epoch.ChannelId, OnUpdate,
            () => Load(epoch).NoAwait());

        await Load(epoch);
        _isInitialized = true;
    }

    private async Task Load(Epoch epoch)
    {
        var state = await _orleans.StateStorage.Read<AddressableStateValue>(epoch.Identity);

        lock (_gate)
        {
            if (_epoch != epoch)
                return;

            if (state.IsInitialized == true)
            {
                ApplyLocked(epoch, state);
                return;
            }

            // Nothing stored for this epoch and nothing came on its channel: the value of the previous epoch goes.
            if (epoch.LastApplied == default)
                Set(new T());
        }
    }

    public virtual async Task SetValue(T value)
    {
        await _writeLock.WaitAsync();

        try
        {
            Epoch epoch;
            DateTime previous;

            lock (_gate)
            {
                epoch = _epoch ?? throw new InvalidOperationException("DeploymentState is not initialized with a deploy yet.");
                previous = epoch.LastApplied;
            }

            // Later than every value this host has seen, whatever the clock of the host that wrote it.
            var state = DeploymentStateRow.Create(_orleans.Serializer, value, previous);

            // The value becomes visible only once it is in the database: a failed write leaves the old one.
            await _orleans.StateStorage.Write(epoch.Identity, state);

            lock (_gate)
            {
                if (_epoch == epoch && state.UpdateDate > epoch.LastApplied)
                {
                    epoch.MarkApplied(state.UpdateDate);
                    Set(value);
                }
            }

            await _messaging.PublishChannel(epoch.ChannelId, state);
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

    // A state of the current epoch (a delivery of its channel).
    protected bool TryApplyUpdate(AddressableStateValue state)
    {
        lock (_gate)
        {
            var epoch = _epoch ?? throw new InvalidOperationException("DeploymentState is not initialized with a deploy yet.");
            return ApplyLocked(epoch, state);
        }
    }

    // A state written to the row of the given deploy: dropped when the epoch has changed since the write started.
    protected bool TryApplyUpdate(Guid deployId, AddressableStateValue state)
    {
        lock (_gate)
        {
            if (_epoch == null || _epoch.DeployId != deployId)
                return false;

            return ApplyLocked(_epoch, state);
        }
    }

    private bool ApplyLocked(Epoch epoch, AddressableStateValue state)
    {
        if (state.IsInitialized == false)
            return false;

        if (state.UpdateDate <= epoch.LastApplied)
        {
            _logger.LogDebug(
                "[DeploymentState] Skipped stale update for {Type}: sent {Sent:O}, last applied {Applied:O}",
                typeof(T).Name, state.UpdateDate, epoch.LastApplied);
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
                $"[DeploymentState] Failed to deserialize update for {typeof(T).Name} (deploy {epoch.DeployId})", e);
        }

        epoch.MarkApplied(state.UpdateDate);
        Set(value);
        return true;
    }

    private class Epoch
    {
        public Epoch(Guid deployId, StateIdentity identity, DeploymentStateChannelId<T> channelId)
        {
            DeployId = deployId;
            Identity = identity;
            ChannelId = channelId;
        }

        public Guid DeployId { get; }
        public StateIdentity Identity { get; }
        public DeploymentStateChannelId<T> ChannelId { get; }

        // UpdateDate of the last state applied to Value in this epoch (from the load, SetValue, or a channel
        // update). Channel updates include our own publishes echoed back with messaging latency: without this
        // watermark an old echo rolls Value back and the next read-modify-write (ClusterFeatures.Apply) publishes a
        // state computed from the stale base, clobbering newer flags cluster-wide. Changed under _gate.
        public DateTime LastApplied { get; private set; }

        public void MarkApplied(DateTime updateDate)
        {
            LastApplied = updateDate;
        }
    }
}
