namespace Common.Reactive
{
    // Thread-safe: connections, timers and grain callbacks terminate and listen to lifetimes from different threads.
    // The state is changed under a lock; listeners and the cancellation callbacks are invoked outside of it.
    public class Lifetime : ILifetime
    {
        public Lifetime(IReadOnlyLifetime? parent = null)
        {
            _parent = parent;
        }

        private readonly IReadOnlyLifetime? _parent;
        private readonly Lock _lock = new();

        private List<Action>? _listeners;
        private CancellationTokenSource? _cancellation;
        private volatile bool _isTerminated;

        public CancellationToken Token
        {
            get
            {
                lock (_lock)
                {
                    // No source is created after termination: a cancelled token is enough.
                    if (_isTerminated == true && _cancellation == null)
                        return new CancellationToken(canceled: true);

                    _cancellation ??= new CancellationTokenSource();
                    return _cancellation.Token;
                }
            }
        }

        public bool IsTerminated => _isTerminated;

        public void Listen(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            lock (_lock)
            {
                if (_isTerminated == false)
                {
                    _listeners ??= new List<Action>();
                    _listeners.Add(callback);
                    return;
                }
            }

            callback.Invoke();
        }

        public void RemoveListener(Action callback)
        {
            lock (_lock)
                _listeners?.Remove(callback);
        }

        public void Terminate()
        {
            List<Action>? listeners;
            CancellationTokenSource? cancellation;

            // One pass over the taken list: Listen/RemoveListener from listeners or other threads do not touch it.
            lock (_lock)
            {
                if (_isTerminated == true)
                    return;

                _isTerminated = true;
                listeners = _listeners;
                _listeners = null;
                cancellation = _cancellation;
            }

            cancellation?.Cancel();

            if (listeners != null)
            {
                foreach (var listener in listeners)
                    listener.Invoke();
            }

            _parent?.RemoveListener(Terminate);
        }
    }
}
