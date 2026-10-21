using System;
using System.Threading;

namespace Internal
{
    // A lifetime that has already ended: it behaves as a terminated Lifetime. A listener is invoked right away
    // (a subscription made on it is undone at once instead of living forever), the token is cancelled.
    public class TerminatedLifetime : ILifetime
    {
        public CancellationToken Token => new(canceled: true);
        public bool IsTerminated => true;

        public void Listen(Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            callback.Invoke();
        }

        public void RemoveListener(Action callback)
        {
        }

        public void RemoveTerminationListener(Action callback)
        {
        }

        public ILifetime CreateChild()
        {
            return new TerminatedLifetime();
        }

        public void Terminate()
        {
        }
    }
}