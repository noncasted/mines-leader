using System;
using System.Collections.Generic;

namespace Internal
{
    public class LifetimedValue<T> : ILifetimedValue<T>
    {
        // The value exists from the start, also for the default of a value type: Set skips an equal value, so going
        // through it here would leave no value for (false), (0) and the like.
        public LifetimedValue(T value)
        {
            _value = value;
        }

        public LifetimedValue() : this(default)
        {
        }

        private readonly ValueEventSource _eventSource = new();

        private T _value;

        // The lifetime of the current value, made when it is first needed (ValueLifetime, a listener of a change): a
        // value changed every frame that nobody listens to and nobody asks the lifetime of allocates nothing.
        private Lifetime _lifetime;

        public T Value => _value;
        public IReadOnlyLifetime ValueLifetime => _lifetime ??= new Lifetime();

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        // An equal value is not a change: no notification, the value lifetime stays. Null again is equal too.
        public void Set(T value)
        {
            if (EqualityComparer<T>.Default.Equals(_value, value) == true)
                return;

            _value = value;

            Notify();
        }

        // The value changed in place (a mutable one, a collection inside it): the value lifetime is renewed and the
        // listeners get the current value, as after Set.
        public void Notify()
        {
            _lifetime?.Terminate();
            _lifetime = null;

            if (_eventSource.ListenersCount == 0)
                return;

            _eventSource.Invoke(ValueLifetime, _value);
        }

        // The listeners get the current value and its lifetime once more, nothing is renewed: for the ones already
        // advised (a late one catches up through View).
        public void InvokeAdvices()
        {
            _eventSource.Invoke(ValueLifetime, _value);
        }

        // The lifetime of the value ends and stays: ValueLifetime reads as ended after it.
        public void Dispose()
        {
            ((Lifetime)ValueLifetime).Terminate();
            _eventSource.Dispose();
        }

        // A handler may set a new value during the pass: the nested pass delivers it to everyone, and the outer one
        // stops instead of delivering the superseded value (with its ended lifetime) to the handlers after it.
        private sealed class ValueEventSource : EventSourceBase<Action<IReadOnlyLifetime, T>>
        {
            public void Invoke(IReadOnlyLifetime lifetime, T value)
            {
                List<Exception> exceptions = null;

                foreach (var listener in Listeners)
                {
                    if (lifetime.IsTerminated == true)
                        break;

                    try
                    {
                        listener.Handler.Invoke(lifetime, value);
                    }
                    catch (Exception exception)
                    {
                        Keep(ref exceptions, exception);
                    }
                }

                Report(exceptions);
            }
        }
    }
}
