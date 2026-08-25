using System;
using Animations;
using Cysharp.Threading.Tasks;
using Internal;
using Tools;
using UnityEngine;
using VContainer.Internal;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellAnimator : MonoBehaviour, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        private ForwardSpriteAnimation _mineExplosion;
        private ForwardSpriteAnimation _zipZapExplosion;
        private ForwardSpriteAnimation _cellOpen;

        public bool IsPlaying =>
            PlayingKind != null;

        public string PlayingKind
        {
            get
            {
                if (_mineExplosion != null && _mineExplosion.IsPlaying)
                    return "explosion";

                if (_zipZapExplosion != null && _zipZapExplosion.IsPlaying)
                    return "explosion";

                if (_cellOpen != null && _cellOpen.IsPlaying)
                    return "open";

                return null;
            }
        }

        public void Construct(IUpdater updater)
        {
            _mineExplosion = Create(Sprites.GameCells.GameCellsExplosionNormal);
            _zipZapExplosion = Create(Sprites.GameCells.GameCellsExplosionElectric);
            _cellOpen = Create(Sprites.GameCells.GameCellsOpen);

            return;

            ForwardSpriteAnimation Create(ISpriteAnimationData data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                    new SpriteAnimationData(data.Sprites, data.Time, data.Color));
            }
        }

        public async UniTask PlayExplosion(IReadOnlyLifetime lifetime, CellExplosionType type)
        {
            Debug.Log("Explode");
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