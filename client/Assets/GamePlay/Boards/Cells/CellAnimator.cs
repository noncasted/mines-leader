using Common.Animations;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer.Internal;

namespace GamePlay.Boards
{
    public class CellExplosion : ForwardSpriteAnimation
    {
        public CellExplosion(Utils utils, ISpriteAnimationData data) : base(utils, data)
        {
        }
    }

    [DisallowMultipleComponent]
    public class CellAnimator : SpriteAnimationRenderer
    {
        [SerializeField] private ForwardAnimationData _explosionData;

        private CellExplosion _explosion;

        public void Construct(IUpdater updater)
        {
            _explosion = new CellExplosion(
                new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                new SpriteAnimationData(_explosionData.Sprites, _explosionData.Time));
        }

        public async UniTask PlayExplosion(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(true);
            var animationLifetime = lifetime.Child();
            _explosion.OnSetup(animationLifetime);
            await _explosion.PlayAsync(lifetime);
            animationLifetime.Terminate();
            gameObject.SetActive(false);
        }
    }
}