using Internal;
using UnityEngine;

namespace Common.Animations
{
    [DisallowMultipleComponent]
    public class SpriteAnimationRenderer : MonoBehaviour, IEntityComponent, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<ISpriteAnimationRenderer>();

            OnRegister(builder);
        }

        public void SetSprite(Sprite sprite)
        {
            _renderer.sprite = sprite;
        }

        protected virtual void OnRegister(IEntityBuilder builder)
        {
        }
    }
}