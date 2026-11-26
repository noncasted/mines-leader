using System;
using System.Collections.Generic;

namespace Internal
{
    public class EventSource : EventSourceBase<Action>, IEventSource
    {
        public void Invoke()
        {
            List<Exception> exceptions = null;

            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Handler.Invoke();
                }
                catch (Exception exception)
                {
                    Keep(ref exceptions, exception);
                }
            }

            Report(exceptions);
        }
    }

    public class EventSource<T> : EventSourceBase<Action<T>>, IEventSource<T>
    {
        public void Invoke(T value)
        {
            List<Exception> exceptions = null;

            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Handler.Invoke(value);
                }
                catch (Exception exception)
                {
                    Keep(ref exceptions, exception);
                }
            }

            Report(exceptions);
        }
    }

    public class EventSource<T1, T2> : EventSourceBase<Action<T1, T2>>, IEventSource<T1, T2>
    {
        public void Invoke(T1 value1, T2 value2)
        {
            List<Exception> exceptions = null;

            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Handler.Invoke(value1, value2);
                }
                catch (Exception exception)
                {
                    Keep(ref exceptions, exception);
                }
            }

            Report(exceptions);
        }
    }

    public class EventSource<T1, T2, T3> : EventSourceBase<Action<T1, T2, T3>>, IEventSource<T1, T2, T3>
    {
        public void Invoke(T1 value1, T2 value2, T3 value3)
        {
            List<Exception> exceptions = null;

            foreach (var listener in Listeners)
            {
                try
                {
                    listener.Handler.Invoke(value1, value2, value3);
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
