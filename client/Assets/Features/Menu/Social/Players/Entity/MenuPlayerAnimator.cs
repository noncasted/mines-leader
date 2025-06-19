using Common.Animations;
using Internal;
using UnityEngine;

namespace Menu
{
    public class MenuPlayerIdle : ForwardSpriteAnimation
    {
        public MenuPlayerIdle(Utils utils, ISpriteAnimationData data) : base(utils, data)
        {
        }
    }

    public class MenuPlayerRun : ForwardSpriteAnimation
    {
        public MenuPlayerRun(Utils utils, ISpriteAnimationData data) : base(utils, data)
        {
        }
    }
    
    [DisallowMultipleComponent]
    public class MenuPlayerAnimator : SpriteAnimationRenderer
    {
        [SerializeField] private ForwardAnimationData _idle;
        [SerializeField] private ForwardAnimationData _run;
        
        protected override void OnRegister(IEntityBuilder builder)
        {
            builder.Register<ForwardSpriteAnimation.Utils>();
            
            builder.RegisterSpriteForwardAnimation<MenuPlayerIdle>(_idle);
            builder.RegisterSpriteForwardAnimation<MenuPlayerRun>(_run);
        }
    }
}