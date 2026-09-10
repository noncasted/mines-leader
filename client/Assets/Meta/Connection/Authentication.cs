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
        private const string UserIdKey = "userId";

        public Guid? Load()
        {
            using (GameProfiler.Scope("Saved user id"))
            {
                var key = GetKey();

                if (PlayerPrefs.HasKey(key) == false)
                    return null;

                if (Guid.TryParse(PlayerPrefs.GetString(key), out var userId) == false)
                {
                    Debug.LogWarning("[Meta] Saved user id is malformed, a new user will be created");
                    return null;
                }

                return userId;
            }
        }

        public void Save(Guid id)
        {
            using (GameProfiler.Scope("Save user id"))
            {
                PlayerPrefs.SetString(GetKey(), id.ToString());

#if UNITY_EDITOR
                PlayerPrefs.SetString(UserIdKey, id.ToString());
#endif
            }
        }

        private static string GetKey()
        {
#if UNITY_EDITOR
            // Копии проекта на диске не должны делить одного юзера, поэтому в редакторе
            // ключ разводится по пути.
            var pathHash = Application.dataPath.GetHashCode();
            return $"{UserIdKey}:{pathHash}";
#else
            return UserIdKey;
#endif
        }
    }
}