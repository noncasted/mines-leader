using System;
using UnityEngine;

namespace Internal
{
    public interface ISceneReloadListener
    {
        bool OnReload();
    }

    public class SceneServicesFactory : MonoBehaviour, ISceneReloadListener
    {
        [SerializeField] private MonoBehaviour[] _services;

        public void Create(IScopeBuilder builder)
        {
            foreach (var service in _services)
            {
                // The list is filled by OnReload with ISceneService components only: anything else (a missing
                // script, a component replaced by hand) is a broken scene.
                if (service is not ISceneService sceneService)
                {
                    var entry = service == null ? "null" : service.GetType().FullName;

                    throw new InvalidOperationException(
                        $"[SceneServicesFactory] {name}: {entry} is not an ISceneService, rescan the scene services");
                }

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