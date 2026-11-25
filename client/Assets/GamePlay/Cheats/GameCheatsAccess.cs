#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Internal;
using Unity.Scripting.LifecycleManagement;

namespace GamePlay.Cheats
{
    // The only static of the cheats: the editor window lives outside every container and reaches the match here.
    [AutoStaticsCleanup]
    public static class GameCheatsAccess
    {
        public static IGameCheats Current { get; private set; }
        public static bool IsActive => Current != null;

        public static IGameCheats Require()
        {
            if (Current == null)
                throw new InvalidOperationException("Not in a match: start a game first");

            return Current;
        }

        public static void Attach(IReadOnlyLifetime lifetime, IGameCheats cheats)
        {
            if (Current != null)
                throw new InvalidOperationException("Cheats of another match are still attached");

            Current = cheats;
            lifetime.Listen(() => Current = null);
        }
    }
}
#endif
