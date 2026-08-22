using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardTargets
    {
        Vector2 LocalSpawn { get; }
        Vector2 LocalStash { get; }
        Vector2 RemoteSpawn { get; }
        Vector2 RemoteStash { get; }
    }

    [DisallowMultipleComponent]
    public class CardTargets : MonoBehaviour, ICardTargets, ISceneService
    {
        [SerializeField] private Transform _localSpawn;
        [SerializeField] private Transform _localStash;
        [SerializeField] private Transform _remoteSpawn;
        [SerializeField] private Transform _remoteStash;

        public Vector2 LocalSpawn => GetPosition(_localSpawn);
        public Vector2 LocalStash => GetPosition(_localStash);
        public Vector2 RemoteSpawn => GetPosition(_remoteSpawn);
        public Vector2 RemoteStash => GetPosition(_remoteStash);

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ICardTargets>();
        }

        private static Vector2 GetPosition(Transform target)
        {
            if (target is RectTransform rectTransform)
                return rectTransform.TransformPoint(rectTransform.rect.center);

            return target.position;
        }
    }
}
