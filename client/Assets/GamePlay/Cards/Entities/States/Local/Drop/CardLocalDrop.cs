using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardLocalDrop
    {
        UniTask Enter();
    }
    
    public class CardLocalDrop : ICardLocalDrop
    {
        public CardLocalDrop(
            IUpdater updater,
            ICardStateContext stateContext,
            IStash stash,
            ILocalCard card,
            ICardTransform transform,
            CardDropOptions options)
        {
            _updater = updater;
            _stash = stash;
            _card = card;
            _transform = transform;
            _options = options;
            _stateContext = stateContext;
        }

        private readonly IUpdater _updater;
        private readonly ICardStateContext _stateContext;
        private readonly IStash _stash;
        private readonly ILocalCard _card;
        private readonly ICardTransform _transform;
        private readonly CardDropOptions _options;

        public async UniTask Enter()
        {
            var lifetime = _stateContext.OccupyLifetime();
            var startPosition = _transform.Position;
            var direction = (_transform.Rotation + 90f).ToAngle().ToVector2();
            var targetPosition = startPosition + direction * _options.MoveDistance;

            var timer = 0f;

            await _updater.RunUpdateAction(lifetime, _options.Time, delta =>
            {
                timer += delta;
                var progress = Mathf.Clamp01(timer / _options.Time);

                var xScale = _options.XScaleCurve.Evaluate(progress);
                var moveFactor = _options.MoveCurve.Evaluate(progress);
                var position = Vector2.Lerp(startPosition, targetPosition, moveFactor);

                _transform.SetScale(new Vector2(xScale, 1f));
                _transform.SetPosition(position);
            });

            _stash.AddCard(_card.Type);
            await _card.Destroy();
        }
    }
}