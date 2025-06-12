using Internal;
using UnityEngine;

namespace Menu
{
    public interface IMenuMoveArea
    {
        RectTransform Transform { get; }
    }
    
    [DisallowMultipleComponent]
    public class MenuMoveArea : MonoBehaviour, ISceneService, IMenuMoveArea
    {
        [SerializeField] private RectTransform _transform;

        public RectTransform Transform => _transform;
        
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IMenuMoveArea>();
        }
    }
}