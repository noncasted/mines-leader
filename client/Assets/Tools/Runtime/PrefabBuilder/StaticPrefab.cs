using UnityEngine;

namespace Tools.Runtime.PrefabBuilder
{
    public class StaticPrefab
    {
        private const string Prefix = "Generated/";
        private readonly string _path;
        private GameObject _cache;

        public StaticPrefab(string path)
        {
            _path = Prefix + path;
        }

        public GameObject Value => _cache ??= Resources.Load<GameObject>(_path);

        public T As<T>() where T : Component => Value.GetComponent<T>();

        public static implicit operator GameObject(StaticPrefab prefab) => prefab.Value;
    }
}