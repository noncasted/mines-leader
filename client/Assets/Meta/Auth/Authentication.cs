using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface IAuthentication
    {
        UniTask<Guid> Execute();
    }

    public class Authentication : IAuthentication
    {
        public Authentication(IMetaBackend backend)
        {
            _backend = backend;
        }

        private readonly IMetaBackend _backend;

        public async UniTask<Guid> Execute()
        {
            Debug.Log("[Meta] Authenticating user...");

            bool hasUserId;
            Guid userId;

            using (GameProfiler.Scope("Saved user id"))
                hasUserId = TryGetUserId(out userId);

            if (hasUserId == true)
            {
                using (GameProfiler.Scope("Log in"))
                    await _backend.LogIn(userId);

                Debug.Log("[Meta] User authenticated with existing ID: " + userId);
                return userId;
            }

            Debug.Log("[Meta] No existing user ID found. Signing up new user...");

            SharedBackendUserSignUp.Response response;

            using (GameProfiler.Scope("Sign up"))
                response = await _backend.SignUp();

            using (GameProfiler.Scope("Save user id"))
            {
#if UNITY_EDITOR
                var pathHash = Application.dataPath.GetHashCode();
                PlayerPrefs.SetString($"userId:{pathHash}", response.Id.ToString());
#endif

                PlayerPrefs.SetString("userId", response.Id.ToString());
            }

            return response.Id;

            bool TryGetUserId(out Guid userId)
            {
#if UNITY_EDITOR
                var pathHash = Application.dataPath.GetHashCode();

                if (PlayerPrefs.HasKey($"userId:{pathHash}") == true)
                {
                    userId = Guid.Parse(PlayerPrefs.GetString("userId"));
                    return true;
                }

                userId = Guid.Empty;
                return false;
#endif

                if (PlayerPrefs.HasKey("userId") == true)
                {
                    userId = Guid.Parse(PlayerPrefs.GetString("userId"));
                    return true;
                }

                userId = Guid.Empty;
                return false;
            }
        }
    }
}