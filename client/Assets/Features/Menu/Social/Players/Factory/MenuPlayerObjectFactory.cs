using Internal;
using UnityEngine;

namespace Menu
{
    public interface IMenuPlayerObjectFactory
    {
        MenuPlayerView Create();
    }
    
    public class MenuPlayerObjectFactory : MonoBehaviour, ISceneService, IMenuPlayerObjectFactory
    {
        [SerializeField] private float _radius = 1f;
        [SerializeField] private Transform _playersRoot;
        [SerializeField] private MenuPlayerView _prefab;
        
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMenuPlayerObjectFactory>();
        }

        public MenuPlayerView Create()
        {
            var position = (Vector2)transform.position + DirectionUtils.Random(0f, _radius);
            var view = Instantiate(_prefab, position, Quaternion.identity, _playersRoot);

            return view;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}