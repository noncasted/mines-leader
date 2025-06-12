using System;

namespace Internal
{
    public class LifetimedValue<T> : ILifetimedValue<T>
    {
        public LifetimedValue(T value)
        {
            Set(value);
        }

        public LifetimedValue()
        {
            Set(default);
        }

        private readonly EventSource<IReadOnlyLifetime, T> _eventSource = new();

        private T _value;
        private Lifetime _lifetime;

        public T Value => _value;
        public IReadOnlyLifetime ValueLifetime => _lifetime;

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _eventSource.Advise(lifetime, handler);
        }

        public void Set(T value)
        {
            if (_value?.Equals(value) == true)
                return;

            _value = value;

            Notify();
        }

        public void Notify()
        {
            _lifetime?.Terminate();
            _lifetime = new Lifetime();
            _eventSource.Invoke(_lifetime, _value);
        }

        public void Dispose()
        {
            _lifetime?.Terminate();
            _eventSource.Dispose();
        }
    }
}