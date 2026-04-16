using System;
using Animations;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer.Internal;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellAnimator : MonoBehaviour, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        [SerializeField] private ForwardAnimationAsset _mineExplosionData;
        [SerializeField] private ForwardAnimationAsset _zipZapExplosionData;
        [SerializeField] private ForwardAnimationAsset _cellOpenData;

        private ForwardSpriteAnimation _mineExplosion;
        private ForwardSpriteAnimation _zipZapExplosion;
        private ForwardSpriteAnimation _cellOpen;

        public void Construct(IUpdater updater)
        {
            _mineExplosion = Create(_mineExplosionData);
            _zipZapExplosion = Create(_zipZapExplosionData);
            _cellOpen = Create(_cellOpenData);

            return;

            ForwardSpriteAnimation Create(ForwardAnimationAsset data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                    new SpriteAnimationData(data.Sprites, data.Time, data.Color));
            }
        }

        public async UniTask PlayExplosion(IReadOnlyLifetime lifetime, CellExplosionType type)
        {
            ForwardSpriteAnimation anim = type switch
            {
                CellExplosionType.ZipZap => _zipZapExplosion,
                CellExplosionType.Mine => _mineExplosion,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();
            anim.OnSetup(animationLifetime);
            await anim.PlayAsync(animationLifetime);
            animationLifetime.Terminate();

            gameObject.SetActive(false);
        }

        public void SetSprite(Sprite sprite)
        {
            _renderer.sprite = sprite;
        }

        public void SetColor(Color color)
        {
            _renderer.color = color;
        }

        public async UniTask PlayOpen(IReadOnlyLifetime lifetime)
        {
            if (_zipZapExplosion.IsPlaying == true || _mineExplosion.IsPlaying == true)
                return;

            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();
            _cellOpen.OnSetup(animationLifetime);
            await _cellOpen.PlayAsync(animationLifetime);
            animationLifetime.Terminate();

            if (_zipZapExplosion.IsPlaying == true || _mineExplosion.IsPlaying == true)
                return;

            gameObject.SetActive(false);
        }
    }
}