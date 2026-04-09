using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardLocalDrop
    {
        UniTask Enter(IReadOnlyLifetime lifetime);
    }

    public class CardLocalDrop : ICardLocalDrop
    {
        public CardLocalDrop(
            IUpdater updater,
            ICardTransform transform,
            CardDropOptions options)
        {
            _updater = updater;
            _transform = transform;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly ICardTransform _transform;
        private readonly CardDropOptions _options;

        public async UniTask Enter(IReadOnlyLifetime lifetime)
        {
            var startPosition = _transform.Position;
            var direction = (_transform.Rotation + 90f).ToAngle().ToVector2();
            var targetPosition = startPosition + direction * _options.MoveDistance;

            var timer = 0f;

            await _updater.RunUpdateAction(lifetime, _options.Time, delta => {
                timer += delta;
                var progress = Mathf.Clamp01(timer / _options.Time);

                var xScale = _options.XScaleCurve.Evaluate(progress);
                var moveFactor = _options.MoveCurve.Evaluate(progress);
                var position = Vector2.Lerp(startPosition, targetPosition, moveFactor);

                _transform.SetScale(new Vector2(xScale, 1f));
                _transform.SetPosition(position);
            });
        }
    }
}