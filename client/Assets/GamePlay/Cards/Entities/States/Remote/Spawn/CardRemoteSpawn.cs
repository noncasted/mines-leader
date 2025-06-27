using Cysharp.Threading.Tasks;
using Global.Systems;
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
            ICardRemoteIdle idle,
            CardRemoteSpawnOptions options)
        {
            _updater = updater;
            _handEntryHandle = handEntryHandle;
            _transform = transform;
            _stateLifetime = stateLifetime;
            _idle = idle;
            _options = options;
        }

        private readonly IUpdater _updater;
        private readonly IHandEntryHandle _handEntryHandle;
        private readonly ICardTransform _transform;
        private readonly ICardStateLifetime _stateLifetime;
        private readonly ICardRemoteIdle _idle;
        private readonly CardRemoteSpawnOptions _options;

        public async UniTask Execute()
        {
            _handEntryHandle.AddToHand();
            var positionHandle = _handEntryHandle.PositionHandle;

            var moveCurve = _options.MoveCurve.CreateInstance();
            var heightCurve = _options.HeightCurve.CreateInstance();
            var rotationCurve = _options.RotationCurve.CreateInstance();

            var startRotation = _transform.Rotation;
            var startPosition = _transform.Position;

            var lifetime = _stateLifetime.OccupyLifetime();

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