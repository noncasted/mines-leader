using System;
using System.Collections.Generic;

namespace Internal
{
    // Not thread-safe: the owner synchronizes (the Unity main thread, the execution queue of a session). Handlers may
    // advise and dispose during a pass: the list defers the changes to its end (ModifiableList).
    public class EventSourceBase<T> : IEventSourceBase<T>
    {
        protected readonly ModifiableList<Subscription> Listeners = new();

        public int ListenersCount => Listeners.Count;

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

        // A single failing handler must not abort delivery to the rest: the exceptions are kept and reported after the
        // pass (ReactiveExceptions).
        protected static void Keep(ref List<Exception> exceptions, Exception exception)
        {
            (exceptions ??= new List<Exception>()).Add(exception);
        }

        protected static void Report(List<Exception> exceptions)
        {
            if (exceptions != null)
                ReactiveExceptions.Report(exceptions);
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
}
