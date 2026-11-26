using Cluster.Discovery;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public class ServiceSlicedState<TData, TSlice> : DeploymentState<TData>, IServiceSlicedState<TData, TSlice>
    where TData : class, IServiceSlices<TSlice>, new()
    where TSlice : class
{
    public ServiceSlicedState(
        IOrleans orleans,
        IMessaging messaging,
        ILoggerFactory loggerFactory,
        IServiceEnvironment environment,
        TData baseValue) : base(orleans, messaging, loggerFactory, baseValue)
    {
        _serviceId = environment.ServiceId;
    }

    private readonly Guid _serviceId;
    private readonly SemaphoreSlim _sliceLock = new(1, 1);
    private readonly Lock _pendingLock = new();
    private TSlice? _pending;

    // A whole-value write from one service would drop the slices of the others.
    public override Task SetValue(TData value)
    {
        throw new NotSupportedException($"{typeof(TData).Name} is changed only through SetSlice");
    }

    public async Task SetSlice(TSlice slice)
    {
        lock (_pendingLock)
            _pending = slice;

        await _sliceLock.WaitAsync();

        try
        {
            TSlice? next;

            lock (_pendingLock)
            {
                next = _pending;
                _pending = null;
            }

            // A call that came later took this slice (or a newer one) while this call waited for the lock.
            if (next == null)
                return;

            await Write(next);
        }
        finally
        {
            _sliceLock.Release();
        }
    }

    private async Task Write(TSlice slice)
    {
        try
        {
            if (IsInitialized == false)
                throw new InvalidOperationException($"{typeof(TData).Name} is not attached to a deploy yet");

            var deployId = _deployId;
            var writer = _orleans.GetGrain<IServiceSlicesWriter<TData, TSlice>>(deployId);
            var state = await writer.SetSlice(_serviceId, slice);

            // The same state also comes back through the channel; whichever arrives first is applied. The result of a
            // write that outlived its epoch belongs to the old deploy row and is dropped.
            TryApplyUpdate(deployId, state);
        }
        catch
        {
            // The slice may have been taken from a call still waiting for the lock: give it back unless a newer one
            // came, so that call writes it instead of returning as if it were saved. The failure goes to this caller.
            lock (_pendingLock)
                _pending ??= slice;

            throw;
        }
    }
}
