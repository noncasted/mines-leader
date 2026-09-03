using System;
using UnityEngine;

namespace Internal
{
    public class EventSourceBase<T> : IEventSourceBase<T>
    {
        protected readonly ModifiableList<T> Listeners = new();

        public void Advise(IReadOnlyLifetime lifetime, T handler)
        {
            Listeners.Add(handler);
            lifetime.Listen(() => Listeners.Remove(handler));
        }

        public void Dispose()
        {
            Listeners.Clear();
        }

        /// <summary>
        /// Один упавший обработчик не должен обрывать рассылку остальным: исключение
        /// логируется, рассылка продолжается.
        /// </summary>
        protected static void Report(Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public class EventSource : EventSourceBase<Action>, IEventSource
    {
        public void Invoke()
        {
            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Invoke();
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }
    }

    public class EventSource<T> : EventSourceBase<Action<T>>, IEventSource<T>
    {
        public void Invoke(T value)
        {
            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Invoke(value);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }
    }

    public class EventSource<T1, T2> : EventSourceBase<Action<T1, T2>>, IEventSource<T1, T2>
    {
        public void Invoke(T1 value1, T2 value2)
        {
            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Invoke(value1, value2);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }
    }

    public class EventSource<T1, T2, T3> : EventSourceBase<Action<T1, T2, T3>>, IEventSource<T1, T2, T3>
    {
        public void Invoke(T1 value1, T2 value2, T3 value3)
        {
            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Invoke(value1, value2, value3);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }
    }
}
