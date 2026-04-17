using Animations;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer.Internal;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellVisuals : MonoBehaviour, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        [SerializeField] private ForwardAnimationAsset _cellTargetData;
        [SerializeField] private ForwardAnimationAsset _cellActionData;

        private ForwardSpriteAnimation _cellTarget;
        private ForwardSpriteAnimation _cellAction;

        public void Construct(IUpdater updater)
        {
            _cellTarget = Create(_cellTargetData);
            _cellAction = Create(_cellActionData);

            return;

            ForwardSpriteAnimation Create(ForwardAnimationAsset data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                    new SpriteAnimationData(data.Sprites, data.Time, data.Color));
            }
        }

        public async UniTask PlayCellTarget(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();

            try
            {
                _cellTarget.OnSetup(animationLifetime);
                await _cellTarget.PlayAsync(animationLifetime);
            }
            finally
            {
                animationLifetime.Terminate();
                if (this != null)
                    gameObject.SetActive(false);
            }
        }

        public async UniTask PlayCellAction(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();

            try
            {
                _cellAction.OnSetup(animationLifetime);
                await _cellAction.PlayAsync(animationLifetime);
            }
            finally
            {
                animationLifetime.Terminate();
                if (this != null)
                    gameObject.SetActive(false);
            }
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
