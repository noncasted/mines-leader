using Common.Animations;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer.Internal;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellAnimator : SpriteAnimationRenderer
    {
        [SerializeField] private ForwardAnimationData _mineExplosionData;
        [SerializeField] private ForwardAnimationData _zipZapExplosionData;

        private ForwardSpriteAnimation _mineExplosion;
        private ForwardSpriteAnimation _zipZapExplosion;

        public void Construct(IUpdater updater)
        {
            _mineExplosion = Create(_mineExplosionData);
            _zipZapExplosion = Create(_zipZapExplosionData);

            return;

            ForwardSpriteAnimation Create(ForwardAnimationData data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                    new SpriteAnimationData(data.Sprites, data.Time));
            }
        }

        public async UniTask PlayExplosion(IReadOnlyLifetime lifetime, CellExplosionType type)
        {
            ForwardSpriteAnimation anim = type switch
            {
                CellExplosionType.ZipZap => _zipZapExplosion,
                CellExplosionType.Mine => _mineExplosion,
                _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
            };

            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();
            anim.OnSetup(animationLifetime);
            await anim.PlayAsync(lifetime);
            animationLifetime.Terminate();

            gameObject.SetActive(false);
        }
    }
}