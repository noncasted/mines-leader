namespace Common.Reactive
{
    public static class LifetimeExtensions
    {
        extension(IReadOnlyLifetime lifetime)
        {
            public ILifetime Child()
            {
                var child = new Lifetime(lifetime);
                lifetime.Listen(child.Terminate);
                return child;
            }

            public ILifetime Intersect(IReadOnlyLifetime lifetimeB)
            {
                var source = lifetime;
                var child = new Lifetime();
                Action terminate = child.Terminate;

                // Whoever ends it (either source or a direct Terminate), the subscriptions leave both sources:
                // otherwise an intersection terminated early stays on the sources until they end.
                child.Listen(() => {
                    source.RemoveListener(terminate);
                    lifetimeB.RemoveListener(terminate);
                });

                source.Listen(terminate);

                // A terminated source ends the intersection at once: the other one is not subscribed to at all.
                if (child.IsTerminated == true)
                    return child;

                lifetimeB.Listen(terminate);

                return child;
            }
        }

        public static IReadOnlyLifetime ToLifetime(this CancellationToken cancellation)
        {
            var child = new Lifetime();
            cancellation.Register(() => child.Terminate());
            return child;
        }
    }
}