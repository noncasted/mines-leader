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

        private List<Action> _listeners;
        private CancellationTokenSource _cancellation;
        private bool _isTerminated;

        public CancellationToken Token
        {
            get
            {
                // После терминации источник не создаётся: отменённый токен и так есть.
                if (_isTerminated == true && _cancellation == null)
                    return new CancellationToken(canceled: true);

                _cancellation ??= new CancellationTokenSource();
                return _cancellation.Token;
            }
        }

        public bool IsTerminated => _isTerminated;

        public void Listen(Action callback)
        {
            if (callback == null)
                return;

            // Сюда же попадает подписка, сделанная слушателем во время Terminate:
            // список к этому моменту уже забран, поэтому колбэк вызывается сразу.
            if (_isTerminated == true)
            {
                Debug.LogError("Trying to listen terminated lifetime");
                callback.Invoke();
                return;
            }

            _listeners ??= new List<Action>();
            _listeners.Add(callback);
        }

        public void RemoveListener(Action callback)
        {
            _listeners?.Remove(callback);
        }

        public void Terminate()
        {
            if (_isTerminated == true)
                return;

            _isTerminated = true;

            _cancellation?.Cancel();

            // Проход одноразовый: список забирается целиком, чтобы Listen/RemoveListener
            // из слушателей не трогали итерируемую коллекцию.
            var listeners = _listeners;
            _listeners = null;

            if (listeners != null)
            {
                foreach (var listener in listeners)
                {
                    // Слушатели освобождают ресурсы, поэтому один упавший не должен
                    // блокировать остальные.
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

            _parent?.RemoveListener(Terminate);
        }
    }
}