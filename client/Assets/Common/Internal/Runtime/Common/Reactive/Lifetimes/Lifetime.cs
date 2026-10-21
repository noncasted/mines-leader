using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Internal
{
    public class Lifetime : ILifetime
    {
        public Lifetime(IReadOnlyLifetime parent = null)
        {
            _parent = parent;
        }

        private readonly IReadOnlyLifetime _parent;

        // Subscription to the parent: created once in Child(), removed in Terminate by the same instance.
        private Action _terminate;

        // The state is guarded by a lock on this instance: a lifetime may be terminated on one thread while another
        // one listens or asks for the token (network threads, thread pool continuations). No extra object is
        // allocated for the lock; listeners and the token callbacks are invoked outside of it.
        private List<Action> _listeners;
        private CancellationTokenSource _cancellation;
        private volatile bool _isTerminated;

        public CancellationToken Token
        {
            get
            {
                lock (this)
                {
                    // No source is created after termination: a cancelled token already exists.
                    if (_isTerminated == true && _cancellation == null)
                        return new CancellationToken(canceled: true);

                    _cancellation ??= new CancellationTokenSource();

                    // The source of a terminated lifetime is already cancelled (or is being cancelled right now by
                    // Terminate on another thread): its token reports it.
                    return _cancellation.Token;
                }
            }
        }

        public bool IsTerminated => _isTerminated;

        internal Action TerminateCallback => _terminate ??= Terminate;

        public void Listen(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (this)
            {
                if (_isTerminated == false)
                {
                    _listeners ??= new List<Action>();
                    _listeners.Add(callback);
                    return;
                }
            }

            // A subscription made by a listener during Terminate also ends up here:
            // by then the list has already been taken, so the callback is invoked immediately.
            Debug.LogError("Trying to listen terminated lifetime");
            callback.Invoke();
        }

        public void RemoveListener(Action callback)
        {
            lock (this)
                _listeners?.Remove(callback);
        }

        public void Terminate()
        {
            List<Action> listeners;
            CancellationTokenSource cancellation;

            // The pass is one-shot: the whole list is taken so that Listen/RemoveListener
            // called from listeners (or from other threads) do not touch the collection being iterated.
            lock (this)
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
                {
                    // Listeners release resources, so a single failing one must not
                    // block the rest.
                    try
                    {
                        listener.Invoke();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }

            // An intersection has no parent: its sources are unsubscribed by its own listener (Intersect).
            if (_terminate != null && _parent != null)
                _parent.RemoveListener(_terminate);
        }
    }
}