using Internal;
using UnityEngine;

namespace Menu.Decks
{
    public interface IMenuDeckMoveArea
    {
        RectTransform Transform { get; }
    }

    [DisallowMultipleComponent]
    public class MenuDeckMoveArea : MonoBehaviour, ISceneService, IMenuDeckMoveArea
    {
        [SerializeField] private RectTransform _transform;

        public RectTransform Transform => _transform;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IMenuDeckMoveArea>();
        }
    }
}