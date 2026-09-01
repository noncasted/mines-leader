using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardRemoteSpawn
    {
        UniTask Execute();
    }

    public class CardRemoteSpawn : ICardRemoteSpawn
    {
        public CardRemoteSpawn(
            IUpdater updater,
            IHandEntryHandle handEntryHandle,
            ICardTransform transform,
            ICardStateLifetime stateLifetime,
            ICardRemoteIdle idle)
        {
            _updater = updater;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _idle = idle;
        }

        private readonly IUpdater _updater;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardRemoteIdle _idle;

        public async UniTask Execute()
        {
            var options = GamePlayAssets.CardRemoteSpawnOptions;

            _handEntryHandle.AddToHand();
            var positionHandle = _handEntryHandle.PositionHandle;

            var moveCurve = options.MoveCurve.CreateInstance();
            var heightCurve = options.HeightCurve.CreateInstance();
            var rotationCurve = options.RotationCurve.CreateInstance();

            var startRotation = _transform.Rotation;
            var startPosition = _transform.Position;

            var lifetime = _stateLifetime.OccupyLifetime();

            await _updater.RunUpdateAction(lifetime, options.Time, delta => {
                var moveFactor = moveCurve.StepForward(delta);
                var heightFactor = heightCurve.StepForward(delta);
                var rotationFactor = rotationCurve.StepForward(delta);

                var position = Vector2.Lerp(startPosition, positionHandle.SupposedPosition, moveFactor);
                position.y += heightFactor * options.AddHeight;

                var rotation = Mathf.Lerp(startRotation, positionHandle.SupposedRotation, rotationFactor);
                _transform.SetPosition(position);
                _transform.SetRotation(rotation);
            });

            _idle.Enter();
        }
    }
}