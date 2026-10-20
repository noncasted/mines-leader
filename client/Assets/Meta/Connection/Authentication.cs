using System;
using Internal;
using UnityEngine;

namespace Meta
{
    public interface IAuthentication
    {
        Guid? Load();
        void Save(Guid id);
    }

    /// <summary>
    /// Хранит id юзера между запусками. Самой авторизации здесь больше нет: она уехала
    /// в хендшейк сокета, поэтому лишнего похода в сеть на старте не остаётся.
    /// </summary>
    public class Authentication : IAuthentication
    {
        // The key of the builds before the per-player keys: outside the editor the id alone under "userId". In the
        // editor it was "userId:<string hash>" (a new one every launch on CoreCLR) plus a copy under "userId" -
        // neither is migrated in the editor: an editor user starts anew once. The key is kept after the migration:
        // a rollback to an older build still finds its user.
        private const string LegacyUserIdKey = "userId";

        public Guid? Load()
        {
            using (GameProfiler.Scope("Saved user id"))
            {
                var key = LocalStorageKeys.Get(LocalStorageKeys.UserId);

                if (PlayerPrefs.HasKey(key) == false)
                    return LoadLegacy();

                return Parse(PlayerPrefs.GetString(key));
            }
        }

        public void Save(Guid id)
        {
            using (GameProfiler.Scope("Save user id"))
            {
                PlayerPrefs.SetString(LocalStorageKeys.Get(LocalStorageKeys.UserId), id.ToString());

                // On the web an unflushed value is lost with the tab: the next launch would create a new user.
                PlayerPrefs.Save();
            }
        }

        private static Guid? LoadLegacy()
        {
#if UNITY_EDITOR
            return null;
#else
            if (PlayerPrefs.HasKey(LegacyUserIdKey) == false)
                return null;

            return Parse(PlayerPrefs.GetString(LegacyUserIdKey));
#endif
        }

        private static Guid? Parse(string raw)
        {
            if (Guid.TryParse(raw, out var userId) == false)
            {
                Debug.LogWarning("[Meta] Saved user id is malformed, a new user will be created");
                return null;
            }

            return userId;
        }
    }
}
