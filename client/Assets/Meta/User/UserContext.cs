using System;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using UnityEngine;

namespace Assets.Meta
{
    public class UserContext : IUserContext
    {
        public UserContext(IBackend backend)
        {
            _backend = backend;
        }

        private readonly IBackend _backend;

        private Guid _id;

        public Guid Id => _id;
        
        public async UniTask Init(IReadOnlyLifetime lifetime)
        {
            _id = await _backend.Auth(lifetime);
            Debug.Log($"Authenticated as {_id}");
        }
    }
}