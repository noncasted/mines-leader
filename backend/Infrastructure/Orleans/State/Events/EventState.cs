using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Common.Extensions;

namespace Infrastructure.State;

public class EventState<TAggregate> : IGrainEventTransactionParticipant
    where TAggregate : class, IEventStateValue, new()
{
    public EventState(
        IEventStorage eventStorage,
        IGrainContext context,
        IGrainStatesRegistry statesRegistry,
        IStateSerializer stateSerializer)
    {
        _eventStorage = eventStorage;
        _context = context;
        _statesRegistry = statesRegistry;
        _stateSerializer = stateSerializer;
    }

    private readonly IEventStorage _eventStorage;
    private readonly IGrainContext _context;
    private readonly IGrainStatesRegistry _statesRegistry;
    private readonly IStateSerializer _stateSerializer;

    private readonly SemaphoreSlim _lock = new(1, 1);

    private TAggregate? _value;
    private Guid _currentTransactionId;
    private readonly List<object> _pendingEvents = [];
    private static readonly ConcurrentDictionary<(Type, Type), Action<object, object>> _applyCache = new();

    public string StreamId => BuildStreamId();

    public TAggregate Value => _value.ThrowIfNull();

    public async Task<TAggregate> Read()
    {
        await Load();
        return _value;
    }

    public async Task Load()
    {
        if (TransactionContextProvider.Current == null)
        {
            await _lock.WaitAsync();

            try
            {
                if (_value != null)
                {
                    _pendingEvents.Clear();
                    return;
                }

                _value = await _eventStorage.Read<TAggregate>(StreamId);
                _pendingEvents.Clear();
            }
            finally
            {
                _lock.Release();
            }

            return;
        }

        if (_currentTransactionId != Guid.Empty && TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        if (_currentTransactionId == TransactionContextProvider.Current.Id)
            return;

        _currentTransactionId = TransactionContextProvider.Current.Id;
        _value = await _eventStorage.Read<TAggregate>(StreamId);
        _pendingEvents.Clear();

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordEventStateChanged(this);
    }

    public async Task Append(params object[] events)
    {
        if (_value == null)
            throw new InvalidOperationException(
                "[EventState] Append called before Read(). State must be loaded before appending events.");

        if (events.Length == 0)
            return;

        if (TransactionContextProvider.Current == null)
        {
            await _lock.WaitAsync();

            try
            {
                _pendingEvents.AddRange(events);
                ApplyEvents(events);
            }
            finally
            {
                _lock.Release();
            }

            return;
        }

        if (_currentTransactionId != Guid.Empty && TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        if (_currentTransactionId == Guid.Empty)
            _currentTransactionId = TransactionContextProvider.Current.Id;

        _pendingEvents.AddRange(events);
        ApplyEvents(events);
    }

    public async Task Write()
    {
        if (_pendingEvents.Count == 0)
            return;

        if (TransactionContextProvider.Current == null)
        {
            await _lock.WaitAsync();

            try
            {
                if (_pendingEvents.Count == 0)
                    return;

                await _eventStorage.Append(StreamId, _pendingEvents.ToArray());
                _pendingEvents.Clear();
            }
            finally
            {
                _lock.Release();
            }

            return;
        }

        if (_currentTransactionId == Guid.Empty)
            throw new InvalidOperationException("[EventState] Write called before Read in a transaction.");

        if (TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();

        var pending = _pendingEvents
                      .Select(e => new EventPayload
                      {
                          Type = e.GetType().AssemblyQualifiedName!,
                          Json = _stateSerializer.Serialize(e)
                      })
                      .ToList();

        handler.RecordEventStateChanged(this, pending);
        _pendingEvents.Clear();
    }

    public void OnTransactionSuccess()
    {
        _pendingEvents.Clear();
        _currentTransactionId = Guid.Empty;
    }

    public void OnTransactionFailure()
    {
        _value = null;
        _pendingEvents.Clear();
        _currentTransactionId = Guid.Empty;
    }

    private void ApplyEvents(object[] events)
    {
        foreach (var e in events)
        {
            var apply = _applyCache.GetOrAdd((_value!.GetType(), e.GetType()), static key => {
                var (aggType, evtType) = key;

                var applyMethod = aggType
                    .GetMethod("Apply",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { evtType },
                        null);

                if (applyMethod == null)
                {
                    throw new InvalidOperationException(
                        $"No Apply({evtType.Name}) method found on aggregate {aggType.Name}. " +
                        $"Ensure the aggregate has a public instance method 'void Apply({evtType.Name})'.");
                }

                var aggParam = Expression.Parameter(typeof(object));
                var evtParam = Expression.Parameter(typeof(object));

                var call = Expression.Call(Expression.Convert(aggParam, aggType),
                    applyMethod,
                    Expression.Convert(evtParam, evtType));

                return Expression.Lambda<Action<object, object>>(call, aggParam, evtParam).Compile();
            });

            apply(_value!, e);
        }
    }

    private string BuildStreamId()
    {
        var stateInfo = _statesRegistry.Get<TAggregate>();
        var key = _context.GrainId.Key.ToString()!;

        if (Guid.TryParse(key, out var guid))
            return $"{stateInfo.Name}:{guid:D}";

        return $"{stateInfo.Name}:{key}";
    }
}