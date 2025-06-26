using Internal;
using UnityEngine;

namespace Common.Objects
{
    public interface IObjectFactory<T>  where T : MonoBehaviour
    {
        Transform Transform { get; }
        
        T Create(T prefab, Vector2 position, float angle);
        T Create(T prefab);

    }

    public static class ObjectFactoryExtensions
    {
        public static T Create<T>(this IObjectFactory<T> factory, T prefab, Vector2 position) where T : MonoBehaviour
        {
            return factory.Create(prefab, position, 0f);
        }
    }
    
    public abstract class ObjectFactory<T> : MonoBehaviour, ISceneService, IObjectFactory<T> where T : MonoBehaviour
    {
        private int _counter;
        
        public Transform Transform => transform;    

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IObjectFactory<T>>();
        }

        public T Create(T prefab, Vector2 position, float angle)
        {
            var instance = Instantiate(prefab, position, Quaternion.Euler(0, 0, angle), transform);
            _counter++;
            instance.name = $"{prefab.name}_{_counter}";
            return instance;
        }

        public T Create(T prefab)
        {
            return Create(prefab, Vector2.zero, 0f);
        }
    }
}