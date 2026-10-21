using System;
using Internal;
using UnityEngine;

namespace Meta
{
    // A guest of the meta saved on this device (S3): the id and the secret the server issued once.
    public readonly struct GuestCredentials
    {
        public GuestCredentials(Guid id, string secret)
        {
            if (id == Guid.Empty || string.IsNullOrEmpty(secret) == true)
                throw new ArgumentException("[Meta] Guest credentials need an id and a secret");

            Id = id;
            Secret = secret;
        }

        public Guid Id { get; }
        public string Secret { get; }
    }

    public interface IAuthentication
    {
        // Null: no guest of this backend environment on this device (the server creates a new one).
        GuestCredentials? LoadGuest();

        // Saves and flushes at once: a secret lost with the tab is a lost account.
        void SaveGuest(GuestCredentials credentials);

        // A user id saved by a build before the guest secret, if any. Such a user cannot be entered any more (no
        // migration, decision of 2026-10-21): only reported when a new guest replaces it.
        Guid? LoadLegacyUserId();
    }

    /// <summary>
    /// The guest of this device in PlayerPrefs: one record under <see cref="LocalStorageKeys.Guest"/> per backend
    /// environment (a local guest does not overwrite the production one in the editor) and per player scope (project
    /// copy / MPPM clone in the editor, one per device outside it).
    /// </summary>
    public class Authentication : IAuthentication
    {
        public Authentication(BackendOptions options)
        {
            _options = options;
        }

        // The key of the builds before the per-player keys: the id alone. Outside the editor only (in the editor the
        // old keys were per process on CoreCLR). Left in place: a rollback to such a build still finds its user.
        private const string BuildLegacyUserIdKey = "userId";

        // The user id of the builds before the guest secret (N12 key, "<name>:<scope>"). Not read for the login any
        // more (such a user cannot be entered): only reported.
        private const string LegacyUserIdKey = "Auth.UserId";

        private readonly BackendOptions _options;

        private string GuestKey => LocalStorageKeys.Get(LocalStorageKeys.Guest, _options.Target);

        public GuestCredentials? LoadGuest()
        {
            using (GameProfiler.Scope("Saved guest"))
            {
                var key = GuestKey;

                if (PlayerPrefs.HasKey(key) == false)
                    return null;

                var record = JsonUtility.FromJson<GuestRecord>(PlayerPrefs.GetString(key));

                if (record == null || Guid.TryParse(record.Id, out var id) == false || string.IsNullOrEmpty(record.Secret) == true)
                    throw new InvalidOperationException($"[Meta] The saved guest under {key} is malformed");

                return new GuestCredentials(id, record.Secret);
            }
        }

        public void SaveGuest(GuestCredentials credentials)
        {
            using (GameProfiler.Scope("Save guest"))
            {
                var record = new GuestRecord
                {
                    Id = credentials.Id.ToString(),
                    Secret = credentials.Secret
                };

                PlayerPrefs.SetString(GuestKey, JsonUtility.ToJson(record));

                // On the web an unflushed value is lost with the tab: the next launch would get a new guest.
                PlayerPrefs.Save();
            }
        }

        public Guid? LoadLegacyUserId()
        {
            var key = LocalStorageKeys.Get(LegacyUserIdKey);

#if !UNITY_EDITOR
            if (PlayerPrefs.HasKey(key) == false)
                key = BuildLegacyUserIdKey;
#endif

            if (PlayerPrefs.HasKey(key) == false)
                return null;

            if (Guid.TryParse(PlayerPrefs.GetString(key), out var userId) == false)
                return null;

            return userId;
        }

        [Serializable]
        private class GuestRecord
        {
            public string Id;
            public string Secret;
        }
    }
}
