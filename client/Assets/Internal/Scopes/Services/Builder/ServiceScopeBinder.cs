using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Internal
{
    public class ServiceScopeBinder : IServiceScopeBinder
    {
        private readonly Scene _scene;

        public ServiceScopeBinder(SceneInstance scene)
        {
            _scene = scene.Scene;
        }

        public void MoveToModules(MonoBehaviour service)
        {
            SceneManager.MoveGameObjectToScene(service.gameObject, _scene);
        }

        public void MoveToModules(GameObject gameObject)
        {
            SceneManager.MoveGameObjectToScene(gameObject, _scene);
        }

        public void MoveToModules(Transform transform)
        {
            SceneManager.MoveGameObjectToScene(transform.gameObject, _scene);
        }
    }
}