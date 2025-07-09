using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class ZipZapLine : MonoBehaviour
    {
        [SerializeField] private LineRenderer _line;
        [SerializeField] private Curve _curve;

        private IUpdater _updater;

        [Inject]
        private void Construct(IUpdater updater)
        {
            _updater = updater;
        }

        public async UniTask Show(IReadOnlyLifetime lifetime, IBoardCell start, IBoardCell end)
        {
            if (start == null || end == null || lifetime.IsTerminated)
                return;

            _line.positionCount = 2;
            _line.SetPosition(0, start.WorldPosition);
            _line.SetPosition(1, start.WorldPosition);

            await _updater.CurveProgression(lifetime, _curve, factor =>
            {
                var position = Vector3.Lerp(start.WorldPosition, end.WorldPosition, factor);
                _line.SetPosition(1, position);
            });

            _line.enabled = true;
        }
    }
}