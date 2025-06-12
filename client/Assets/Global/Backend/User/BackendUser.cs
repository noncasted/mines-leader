using System;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Global.Backend
{
    public class BackendUser : IBackendUser, IBackendProjection<BackendUserContexts.ProfileProjection>
    {
        private readonly ViewableProperty<string> _name = new();

        private Guid _id;

        public Guid Id => _id;
        public IViewableProperty<string> Name => _name;

        public UniTask OnReceived(BackendUserContexts.ProfileProjection data)
        {
            _id = data.Id;
            _name.Set(data.Name);
            
            return UniTask.CompletedTask;
        }
    }
}