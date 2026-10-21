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
        // The guest of this device (Meta.Authentication, S3): its id and the secret the server issued, one record per
        // backend environment - a guest of the local backend is not a guest of production and must not overwrite it.
        public const string Guest = "Auth.Guest";

        // The user id of the builds before the guest secret (N12 key, "<name>:<scope>"). Not read for the login any
        // more (such a user cannot be entered): only reported and shown in Project Tools.
        public const string LegacyUserId = "Auth.UserId";

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

        // "<name>.<environment>:<scope>": a record that belongs to one backend environment.
        public static string Get(string name, BackendEnvironment environment)
        {
            return $"{name}.{environment}:{Scope}";
        }
    }
}
