using System;
using UnityEngine;

namespace Internal
{
    public class EventSourceBase<T> : IEventSourceBase<T>
    {
        protected readonly ModifiableList<Subscription> Listeners = new();

        // Every Advise is its own entry, removed by reference: the same handler advised twice keeps both
        // subscriptions, and the end of one lifetime removes exactly its own one.
        public void Advise(IReadOnlyLifetime lifetime, T handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = new Subscription(Listeners, handler);
            Listeners.Add(subscription);
            lifetime.Listen(subscription.Remove);
        }

        public void Dispose()
        {
            Listeners.Clear();
        }

        /// <summary>
        /// A single failing handler must not abort delivery to the rest: the exception
        /// is logged and delivery continues.
        /// </summary>
        protected static void Report(Exception exception)
        {
            Debug.LogException(exception);
        }

        protected sealed class Subscription
        {
            public Subscription(ModifiableList<Subscription> owner, T handler)
            {
                _owner = owner;
                Handler = handler;
            }

            private readonly ModifiableList<Subscription> _owner;

            public readonly T Handler;

            public void Remove()
            {
                _owner.Remove(this);
            }
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
                    listener.Handler.Invoke();
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
                    listener.Handler.Invoke(value);
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
                    listener.Handler.Invoke(value1, value2);
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
                    listener.Handler.Invoke(value1, value2, value3);
                }
                catch (Exception exception)
                {
                    Report(exception);
                }
            }
        }
    }
}