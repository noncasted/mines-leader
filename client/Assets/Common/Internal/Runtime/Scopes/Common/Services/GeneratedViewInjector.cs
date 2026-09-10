using UnityEngine;

namespace Internal
{
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
