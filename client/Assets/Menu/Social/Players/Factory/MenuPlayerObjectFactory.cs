using Internal;
using Tools;
using UnityEngine;

namespace Menu.Social
{
    public interface IMenuPlayerObjectFactory
    {
        MenuPlayerView Create();
        MenuPlayerView Create(Vector2 position);
    }

    public class MenuPlayerObjectFactory : MonoBehaviour, ISceneService, IMenuPlayerObjectFactory
    {
        [SerializeField] private float _radius = 1f;
        [SerializeField] private Transform _playersRoot;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMenuPlayerObjectFactory>();
        }

        public MenuPlayerView Create()
        {
            var position = (Vector2)transform.position + DirectionUtils.Random(0f, _radius);
            var view = Instantiate(Prefabs.MenuPlayer.As<MenuPlayerView>(), position, Quaternion.identity, _playersRoot);
            return view;
        }

        public MenuPlayerView Create(Vector2 position)
        {
            var view = Instantiate(Prefabs.MenuPlayer.As<MenuPlayerView>(), position, Quaternion.identity, _playersRoot);
            return view;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}