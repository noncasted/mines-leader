using System.Threading;

namespace Internal
{
    public static class LifetimeExtensions
    {
        public static ILifetime Child(this IReadOnlyLifetime lifetime)
        {
            var child = new Lifetime(lifetime);
            lifetime.Listen(child.TerminateCallback);
            return child;
        }

        public static ILifetime Intersect(this IReadOnlyLifetime lifetimeA, IReadOnlyLifetime lifetimeB)
        {
            var child = Lifetime.CreateIntersection(lifetimeA, lifetimeB);

            // Whoever ends it (either source or a direct Terminate), the subscriptions leave both sources:
            // otherwise an intersection terminated early stays on the sources until they end.
            // A source that is terminating is skipped, as for a child (Lifetime.Terminate).
            child.Listen(() => {
                if (lifetimeA.IsTerminated == false)
                    lifetimeA.RemoveListener(child.TerminateCallback);

                if (lifetimeB.IsTerminated == false)
                    lifetimeB.RemoveListener(child.TerminateCallback);
            });

            lifetimeA.Listen(child.TerminateCallback);

            // A terminated source ends the intersection at once: the other one is not subscribed to at all.
            if (child.IsTerminated == true)
                return child;

            lifetimeB.Listen(child.TerminateCallback);

            return child;
        }

        // A lifetime that ends with the cancellation of the token (and leaves the token when it ends).
        public static IReadOnlyLifetime ToLifetime(this CancellationToken cancellation)
        {
            var lifetime = new Lifetime();
            var registration = cancellation.Register(lifetime.Terminate);
            lifetime.Listen(registration.Dispose);
            return lifetime;
        }
    }
}
