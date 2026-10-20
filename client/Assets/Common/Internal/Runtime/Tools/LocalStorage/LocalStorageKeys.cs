using Shared;
using UnityEngine;

namespace Internal
{
    // The keys of PlayerPrefs (IndexedDB on the web) that belong to one player of this device: "<name>:<scope>".
    // - In the editor the scope is a stable hash of Application.dataPath: every copy of the project and every MPPM
    //   clone (it runs from Library/VP/<clone>) is its own player, and the same one between launches
    //   (string.GetHashCode is randomized per process on CoreCLR - it gave a new user on every editor start).
    // - Outside the editor the scope is a constant: the page path never splits the player.
    // Every write is followed by PlayerPrefs.Save(): on the web an unsaved value is lost with the tab.
    public static class LocalStorageKeys
    {
        // The meta user of this device (Meta.Authentication): its id.
        public const string UserId = "Auth.UserId";

        private const string PlayerScope = "player";

        public static string Scope
        {
            get
            {
#if UNITY_EDITOR
                return StableHash.Get(Application.dataPath).ToString("x8");
#else
                return PlayerScope;
#endif
            }
        }

        public static string Get(string name)
        {
            return $"{name}:{Scope}";
        }
    }
}
