#if UNITY_EDITOR
using UnityEngine;

namespace Tools
{
    public static class PrefabsExtensions
    {
        public static T As<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>();
        }
    }
}
#endif