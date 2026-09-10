using UnityEngine;
using UnityEngine.SceneManagement;

namespace Internal
{
    public interface IServiceScopeBinder
    {
        void MoveToModules(MonoBehaviour service);
        void MoveToModules(GameObject service);
        void MoveToModules(Transform service);
    }
    
    public class ServiceScopeBinder : IServiceScopeBinder
    {
        private readonly Scene _scene;

        public ServiceScopeBinder(Scene scene)
        {
            _scene = scene;
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