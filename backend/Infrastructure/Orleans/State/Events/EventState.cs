using System.Collections.Concurrent;
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

    private TAggregate? _value;
    private Guid _currentTransactionId;
    private readonly List<object> _pendingEvents = [];
    private bool _isSessionActive;
    private static readonly ConcurrentDictionary<(Type, Type), Action<object, object>?> _applyCache = new();

    public string StreamId => BuildStreamId();

    public TAggregate Value => _value.ThrowIfNull();

    public async Task Read()
    {
        if (TransactionContextProvider.Current == null)
        {
            if (_value != null)
                return;

            _value = await _eventStorage.Read<TAggregate>(StreamId);
            return;
        }

        if (_currentTransactionId != Guid.Empty && TransactionContextProvider.Current.Id != _currentTransactionId)
            throw new InvalidOperationException("Concurrent transactions are not supported.");

        if (_currentTransactionId == TransactionContextProvider.Current.Id)
            return;

        _currentTransactionId = TransactionContextProvider.Current.Id;
        _value = await _eventStorage.Read<TAggregate>(StreamId);

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordEventStateChanged(this);
    }

    public Task Append(params object[] events)
    {
        if (TransactionContextProvider.Current == null)
        {
            if (!_isSessionActive)
                throw new InvalidOperationException(
                    "[EventState] Append called outside of session. Call StartSession() first.");
        }
        else
        {
            if (_currentTransactionId != Guid.Empty && TransactionContextProvider.Current.Id != _currentTransactionId)
                throw new InvalidOperationException("Concurrent transactions are not supported.");

            if (_currentTransactionId == Guid.Empty)
                _currentTransactionId = TransactionContextProvider.Current.Id;
        }

        _pendingEvents.AddRange(events);

        if (_value == null)
            throw new InvalidOperationException("[EventState] Append called outside of session.");

        foreach (var e in events)
        {
            var apply = _applyCache.GetOrAdd((_value.GetType(), e.GetType()), static key => {
                var (aggType, evtType) = key;

                var applyMethod = aggType
                    .GetMethod("Apply",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { evtType },
                        null);

                if (applyMethod == null)
                    return null;

                var aggParam = System.Linq.Expressions.Expression.Parameter(typeof(object));
                var evtParam = System.Linq.Expressions.Expression.Parameter(typeof(object));

                var call = System.Linq.Expressions.Expression.Call(
                    System.Linq.Expressions.Expression.Convert(aggParam, aggType),
                    applyMethod,
                    System.Linq.Expressions.Expression.Convert(evtParam, evtType));

                return System.Linq.Expressions.Expression.Lambda<Action<object, object>>(call, aggParam, evtParam)
                             .Compile();
            });
            apply?.Invoke(_value, e);
        }

        return Task.CompletedTask;
    }

    public void StartSession()
    {
        if (_pendingEvents.Count != 0)
            throw new InvalidOperationException("[EventState] StartSession called with pending events.");

        _isSessionActive = true;
    }

    public async Task WriteSession()
    {
        if (_pendingEvents.Count == 0)
            return;

        if (TransactionContextProvider.Current == null)
        {
            await _eventStorage.Append(StreamId, _pendingEvents.ToArray());
            _pendingEvents.Clear();
            _isSessionActive = false;
            return;
        }

        var handler = (GrainTransactionHandler)_context.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordEventStateChanged(this);
    }

    public IReadOnlyList<EventPayload> GetPendingEvents()
    {
        return _pendingEvents
               .Select(e => new EventPayload
               {
                   Type = e.GetType().AssemblyQualifiedName!,
                   Json = _stateSerializer.Serialize(e)
               })
               .ToList();
    }

    public IStateValue? GetAggregate()
    {
        return _value;
    }

    public void OnTransactionSuccess()
    {
        _isSessionActive = false;
        _pendingEvents.Clear();
        _currentTransactionId = Guid.Empty;
    }

    public void OnTransactionFailure()
    {
        _isSessionActive = false;
        _value = null;
        _pendingEvents.Clear();
        _currentTransactionId = Guid.Empty;
    }

    private string BuildStreamId()
    {
        var stateInfo = _statesRegistry.Get<TAggregate>();
        var grainIdString = _context.GrainId.ToString();
        var slashIndex = grainIdString.IndexOf('/');
        var key = slashIndex >= 0 ? grainIdString.Substring(slashIndex + 1) : grainIdString;

        if (Guid.TryParse(key, out var guid))
            return $"{stateInfo.Name}:{guid:D}";

        return $"{stateInfo.Name}:{key}";
    }
}