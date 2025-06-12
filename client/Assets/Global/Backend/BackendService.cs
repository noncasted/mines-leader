using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Global.Backend
{
    public class BackendService : IBackend
    {
        public BackendService(
            IBackendClient client,
            BackendOptions options)
        {
            _client = client;
            _options = options;
        }

        private readonly IBackendClient _client;
        private readonly BackendOptions _options;

        public async UniTask<Guid> Auth(IReadOnlyLifetime lifetime)
        {
            if (PlayerPrefs.HasKey("userId") == true)
                return Guid.Parse(PlayerPrefs.GetString("userId"));
            
            var url = _options.Url + "/develop_signUp";

            var request = new BackendAuthContexts.Request()
            {
                Name = "HUESOS"
            };

            var response = await _client.PostJson<BackendAuthContexts.Response, BackendAuthContexts.Request>(
                lifetime,
                url,
                request);
            
            PlayerPrefs.SetString("userId", response.Id.ToString());

            return response.Id;
        }
    }
}