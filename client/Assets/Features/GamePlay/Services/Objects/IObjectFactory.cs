using UnityEngine;

namespace GamePlay.Services
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
}