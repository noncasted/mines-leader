using Common.Extensions;
using Internal;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IAddressableState<T> : IViewableProperty<T> where T : class, new()
{
    bool IsInitialized { get; }

    Task SetValue(T value);
}

public class AddressableState<T> : ViewableProperty<T>, IOrleansStarted, IAddressableState<T>
    where T : class, new()
{
    public AddressableState(AddressableStateUtils utils) : base(new T())
    {
        _orleans = utils.Orleans;
        _messaging = utils.Messaging;
        _logger = utils.LoggerFactory.CreateLogger<AddressableState<T>>();

        var stateInfo = _orleans.StateStorage.Registry.Get<T>();

        _identity = new StateIdentity
        {
            Key = stateInfo.Name,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        _channelId = new AddressableStateChannelId<T>
        {
            Name = _identity.Type
        };
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ILogger<AddressableState<T>> _logger;
    private readonly StateIdentity _identity;
    private readonly AddressableStateChannelId<T> _channelId;

    private const int PublishAttempts = 5;
    private static readonly TimeSpan PublishRetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _applyGate = new();
    private bool _isInitialized;
    private DateTime _updateDate;

    public bool IsInitialized => _isInitialized;
    public DateTime UpdateDate => _updateDate;

    // No catch: a state that failed to load stays uninitialized, and ConfigsInitializer.Init would then overwrite the
    // stored value with the seed. The failure stops the startup instead.
    public async Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        // Listening before the read: an update published while the read runs is not lost, and Apply keeps the newer
        // of the two by UpdateDate. A gap of the channel (missed updates) reloads the value from the database.
        await _messaging.ListenChannel<AddressableStateValue>(lifetime, _channelId, OnUpdate,
            () => Reload().NoAwait());

        await Reload();
    }

    public async Task SetValue(T value)
    {
        await _writeLock.WaitAsync();

        try
        {
            // Later than every value this host has seen, whatever the clock of the host that wrote it: Apply orders
            // the values by this date, a write must never lose to the one it replaces.
            var updateDate = DateTime.UtcNow;

            if (updateDate <= _updateDate)
                updateDate = _updateDate.AddTicks(1);

            var state = new AddressableStateValue()
            {
                IsInitialized = true,
                UpdateDate = updateDate,
                Value = _orleans.Serializer.Serialize(value)
            };

            // The value becomes visible only once it is in the database: a failed write leaves the old one.
            await _orleans.StateStorage.Write(_identity, state);
            Apply(state, value);

            await Publish(state);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[AddressableState] Failed to persist {Type}", typeof(T).Name);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // The channel has no sequence for a message that never got in: without a retry the other hosts keep the old
    // value until their next reload (a gap of the channel or a restart).
    private async Task Publish(AddressableStateValue state)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _messaging.PublishChannel(_channelId, state);
                return;
            }
            catch (Exception e) when (attempt < PublishAttempts)
            {
                _logger.LogWarning(e, "[AddressableState] Publishing {Type} failed (attempt {Attempt}/{Max}), retrying",
                    typeof(T).Name, attempt, PublishAttempts);

                await Task.Delay(PublishRetryDelay * attempt);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} is saved and applied on this host but not published: the other hosts keep the old value until they reload it.",
                    e);
            }
        }
    }

    private async Task Reload()
    {
        var state = await _orleans.StateStorage.Read<AddressableStateValue>(_identity);

        if (state.IsInitialized == false)
        {
            _logger.LogDebug("[AddressableState] {Type} not yet initialized, skipping load", typeof(T).Name);
            return;
        }

        // A value loaded from the database is initialized just like a written one. Without this the flag
        // stayed false until the first update on the channel, and ConfigsInitializer.Init overwrote the
        // stored config with the seed on every start of the coordinator.
        Apply(state, _orleans.Serializer.Deserialize<T>(state.Value));
    }

    private void OnUpdate(AddressableStateValue state)
    {
        Apply(state, _orleans.Serializer.Deserialize<T>(state.Value));
    }

    // The database read and the channel can bring the same or an older value after a newer one (an own write comes
    // back on the channel too): the UpdateDate of the writer orders them.
    private void Apply(AddressableStateValue state, T value)
    {
        lock (_applyGate)
        {
            if (_isInitialized == true && state.UpdateDate <= _updateDate)
                return;

            _isInitialized = true;
            _updateDate = state.UpdateDate;
            Set(value);
        }
    }
}
