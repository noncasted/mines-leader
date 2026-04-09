using Common.Animations;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer.Internal;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class FlagAnimator : MonoBehaviour, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        [SerializeField] private ForwardAnimationAsset _appearData;
        [SerializeField] private ForwardAnimationAsset _removeData;

        private ForwardSpriteAnimation _appear;
        private ForwardSpriteAnimation _remove;

        public void Construct(IUpdater updater)
        {
            _appear = Create(_appearData);
            _remove = Create(_removeData);

            return;

            ForwardSpriteAnimation Create(ForwardAnimationAsset data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                    new SpriteAnimationData(data.Sprites, data.Time)
                );
            }
        }

        public async UniTask PlayAppear(IReadOnlyLifetime lifetime)
        {
            var animationLifetime = lifetime.Child();
            _appear.OnSetup(animationLifetime);
            await _appear.PlayAsync(animationLifetime);
            animationLifetime.Terminate();
        }

        public async UniTask PlayRemove(IReadOnlyLifetime lifetime)
        {
            var animationLifetime = lifetime.Child();
            _remove.OnSetup(animationLifetime);
            await _remove.PlayAsync(animationLifetime);
            animationLifetime.Terminate();
        }

        public void SetSprite(Sprite sprite)
        {
            _renderer.sprite = sprite;
        }
    }
}
