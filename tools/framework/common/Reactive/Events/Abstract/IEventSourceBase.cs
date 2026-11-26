using System;

namespace Internal
{
    public interface IEventSourceBase<T> : IDisposable
    {
        void Advise(IReadOnlyLifetime lifetime, T handler);
    }
}
