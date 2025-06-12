using System;
using Internal;
using Shared;

namespace Global.Backend
{
    public class BackendUser : IBackendUser, IScopeSetup
    {
        public BackendUser(IBackendProjection<BackendUserContexts.ProfileProjection> projection)
        {
            _projection = projection;
        }

        private readonly IBackendProjection<BackendUserContexts.ProfileProjection> _projection;
        private readonly ViewableProperty<string> _name = new();

        private Guid _id;

        public Guid Id => _id;
        public IViewableProperty<string> Name => _name;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _projection.Listen(lifetime, data =>
            {
                _id = data.Id;
                _name.Set(data.Name);
            });
        }
    }
}