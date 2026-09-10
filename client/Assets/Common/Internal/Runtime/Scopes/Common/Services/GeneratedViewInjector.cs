using UnityEngine;

namespace Internal
{
    public interface IViewInjector
    {
        void Inject<T>(T target) where T : MonoBehaviour;
        void Inject(GameObject target);
    }

    public sealed class GeneratedViewInjector : IViewInjector
    {
        public GeneratedViewInjector(IContainer container)
        {
            _container = container;
        }

        private readonly IContainer _container;

        public void Inject<T>(T target) where T : MonoBehaviour
        {
            _container.Inject(target);
        }

        public void Inject(GameObject target)
        {
            _container.InjectGameObject(target);
        }
    }
}