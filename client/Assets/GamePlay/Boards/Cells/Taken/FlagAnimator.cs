using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class FlagAnimator : MonoBehaviour, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        private ForwardSpriteAnimation _show;
        private ForwardSpriteAnimation _hide;

        public bool IsPlaying =>
            (_show != null && _show.IsPlaying) || (_hide != null && _hide.IsPlaying);

        [Inject]
        public void Construct(IUpdater updater)
        {
            _show = Create(Sprites.GameCells.GameCellsFlagShow);
            _hide = Create(Sprites.GameCells.GameCellsFlagHide);

            return;

            ForwardSpriteAnimation Create(ISpriteAnimationData data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, this),
                    new SpriteAnimationData(data.Sprites, data.Time, data.Color));
            }
        }

        public async UniTask PlayAppear(IReadOnlyLifetime lifetime)
        {
            var animationLifetime = lifetime.Child();
            _show.OnSetup(animationLifetime);
            await _show.PlayAsync(animationLifetime);
            animationLifetime.Terminate();
        }

        public async UniTask PlayRemove(IReadOnlyLifetime lifetime)
        {
            var animationLifetime = lifetime.Child();
            _hide.OnSetup(animationLifetime);
            await _hide.PlayAsync(animationLifetime);
            animationLifetime.Terminate();
        }

        public void SetSprite(Sprite sprite)
        {
            _renderer.sprite = sprite;
        }

        public void SetColor(Color color)
        {
            _renderer.color = color;
        }
    }
}