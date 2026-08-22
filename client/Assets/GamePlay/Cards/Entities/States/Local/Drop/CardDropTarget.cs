using GamePlay.Loop;
using Global.Constants;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    public interface ICardDropTarget
    {
        Vector2 Position { get; }
        int DroppedCount { get; }

        Vector2 ReservePosition();
    }

    [DisallowMultipleComponent]
    public class CardDropTarget : MonoBehaviour, ICardDropTarget, ISceneService, IScopeSetup
    {
        [SerializeField] private float _cardHeight = GameConstants.PixelSize;

        private IGameRound _round;
        private int _count;

        public Vector2 Position
        {
            get
            {
                if (transform is RectTransform rectTransform)
                    return rectTransform.TransformPoint(rectTransform.rect.center);

                return transform.position;
            }
        }

        public int DroppedCount => _count;

        [Inject]
        internal void Construct(IGameRound round)
        {
            _round = round;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ICardDropTarget>()
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _round.Player.Advise(lifetime, ResetCount);
        }

        public Vector2 ReservePosition()
        {
            var position = Position + Vector2.up * _cardHeight * _count;
            _count++;
            return position;
        }

        private void ResetCount()
        {
            _count = 0;
        }
    }
}
