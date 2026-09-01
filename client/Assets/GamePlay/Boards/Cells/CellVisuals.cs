using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using VContainer.Internal;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellVisuals : MonoBehaviour, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        private ForwardSpriteAnimation _selection;
        private ForwardSpriteAnimation _cellAction;

        public void Construct(IUpdater updater)
        {
            _selection = Create(Sprites.GameCells.GameCellsHighlightSelect);
            _cellAction = Create(Sprites.GameCells.GameCellsHighlightShow);

            return;

            ForwardSpriteAnimation Create(ISpriteAnimationData data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                    new SpriteAnimationData(data.Sprites, data.Time, data.Color));
            }
        }

        public async UniTask PlaySelection(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();

            try
            {
                _selection.OnSetup(animationLifetime);
                await _selection.PlayAsync(animationLifetime);
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
