using System;
using Cysharp.Threading.Tasks;
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
            var response = await _backend.Auth("HUESOS");
            PlayerPrefs.SetString("userId", response.Id.ToString());
            return response.Id;
        }
    }
}