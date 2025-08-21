using UnityEngine;

namespace Internal
{
    public class SceneServicesFactory : MonoBehaviour, ISceneReloadListener
    {
        [SerializeField] private MonoBehaviour[] _services;

        public void Create(IScopeBuilder builder)
        {
            foreach (var service in _services)
            {
                if (service is not ISceneService sceneService)
                    continue;
                
                sceneService.Create(builder);
            }
        }

        public bool OnReload()
        {
            var newServices = this.GetObjectsWithComponentInScene<ISceneService>();

            if (HasChanged() == false)
                return false;

            _services = newServices;
            return true;
            
            bool HasChanged()
            {
                if (_services.Length != newServices.Length)
                    return true;

                for (var i = 0; i < _services.Length; i++)
                {
                    if (_services[i] != newServices[i])
                        return true;
                }

                return false;
            }
        }
    }
}