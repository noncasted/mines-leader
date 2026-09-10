using System;
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

    /// <summary>
    /// Биндер скоупа без своей сцены: переносить объекты некуда, поэтому любая попытка —
    /// ошибка конфигурации скоупа.
    /// </summary>
    public class ExceptionServiceScopeBinder : IServiceScopeBinder
    {
        private readonly string _scopeName;

        public ExceptionServiceScopeBinder(string scopeName)
        {
            _scopeName = scopeName;
        }

        public void MoveToModules(MonoBehaviour service)
        {
            throw CreateException(service.gameObject);
        }

        public void MoveToModules(GameObject gameObject)
        {
            throw CreateException(gameObject);
        }

        public void MoveToModules(Transform transform)
        {
            throw CreateException(transform.gameObject);
        }

        private Exception CreateException(GameObject gameObject)
        {
            return new InvalidOperationException(
                $"Scope '{_scopeName}' has no service scene to move '{gameObject.name}' into. " +
                "Configure it with WithRuntimeScene or WithAssetScene.");
        }
    }
}