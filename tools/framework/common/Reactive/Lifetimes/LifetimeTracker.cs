#if UNITY_EDITOR
namespace Internal
{
    // The feed of lifetime creation and termination for the Lifetime Graph of the editor: LifetimeAnalytics of the
    // client core installs it. Editor only, the player and the backend compile it out.
    public interface ILifetimeTracker
    {
        // The id of the node; 0 while tracking is off.
        int OnCreated(IReadOnlyLifetime parent, IReadOnlyLifetime secondParent);

        void OnTerminated(int id);
    }

    public static class LifetimeTracker
    {
        public static ILifetimeTracker Current { get; private set; }

        public static void Install(ILifetimeTracker tracker)
        {
            Current = tracker;
        }

        internal static int OnCreated(IReadOnlyLifetime parent, IReadOnlyLifetime secondParent)
        {
            var tracker = Current;

            return tracker == null ? 0 : tracker.OnCreated(parent, secondParent);
        }

        internal static void OnTerminated(int id)
        {
            // Created while tracking was off: not in the graph.
            if (id == 0)
                return;

            Current?.OnTerminated(id);
        }
    }
}
#endif
