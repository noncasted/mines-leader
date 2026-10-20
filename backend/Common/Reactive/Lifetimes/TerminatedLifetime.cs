namespace Common.Reactive
{
    // A lifetime that has already ended: it behaves as a terminated Lifetime. A listener is invoked right away
    // (a subscription made on it is undone at once instead of living forever), the token is cancelled.
    public class TerminatedLifetime : ILifetime
    {
        public CancellationToken Token => new(canceled: true);
        public bool IsTerminated => true;

        public void Listen(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
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