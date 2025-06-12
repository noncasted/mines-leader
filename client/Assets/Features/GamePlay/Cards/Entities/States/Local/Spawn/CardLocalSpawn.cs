using Cysharp.Threading.Tasks;
using Global.Systems;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardLocalSpawn : ICardLocalSpawn
    {
        public CardLocalSpawn(
            IUpdater updater,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardLocalIdle idle,
            ICardStateContext stateContext,
            CardLocalSpawnOptions options)
        {
            _updater = updater;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _idle = idle;
            _stateContext = stateContext;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardLocalIdle _idle;
        private readonly ICardStateContext _stateContext;
        private readonly CardLocalSpawnOptions _options;

        public async UniTask Execute()
        {
            _handEntryHandle.AddToHand();
            var positionHandle = _handEntryHandle.PositionHandle;

            var moveCurve = _options.MoveCurve.CreateInstance();
            var heightCurve = _options.HeightCurve.CreateInstance();
            var rotationCurve = _options.RotationCurve.CreateInstance();

            var startRotation = _transform.Rotation;
            var startPosition = _transform.Position;

            var lifetime = _stateContext.OccupyLifetime();

            await _updater.RunUpdateAction(lifetime, _options.Time, delta =>
            {
                var moveFactor = moveCurve.StepForward(delta);
                var heightFactor = heightCurve.StepForward(delta);
                var rotationFactor = rotationCurve.StepForward(delta);

                var position = Vector2.Lerp(startPosition, positionHandle.SupposedPosition, moveFactor);
                position.y += heightFactor * _options.AddHeight;

                var rotation = Mathf.Lerp(startRotation, positionHandle.SupposedRotation, rotationFactor);
                _transform.SetPosition(position);
                _transform.SetRotation(rotation);
            });

            _idle.Enter();
        }
    }
}