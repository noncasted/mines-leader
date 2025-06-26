using System;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer;

namespace Global.UI
{
    public interface ILoadingScreen
    {
        UniTask Show();
        void Hide();
    }

    [DisallowMultipleComponent]
    public class LoadingScreen : MonoBehaviour, ILoadingScreen, IUpdatable, IScopeSetup
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Curve _curve;
        [SerializeField] private LoadingScreenAnimation _animation;
        
        private IUpdater _updater;
        private CurveInstance _curveInstance;
        private Direction2 _direction;

        [Inject]
        private void Construct(IUpdater updater)
        {
            _updater = updater;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _curveInstance = _curve.CreateInstance();
            _updater.Add(lifetime, this);
        }

        public async UniTask Show()
        {
            _direction = Direction2.Forward;
            await UniTask.WaitUntil(() => _curveInstance.IsFinished == true);
        }

        public void Hide()
        {
            _direction = Direction2.Backward;
        }

        public void OnUpdate(float delta)
        {
            _animation.UpdateAnimation(delta);
            
            var alpha = _direction switch
            {
                Direction2.Forward => _curveInstance.StepForward(delta),
                Direction2.Backward => _curveInstance.StepBack(delta),
                _ => throw new ArgumentOutOfRangeException()
            };

            _group.alpha = alpha;

            if (Mathf.Approximately(alpha, 0f) == true)
                gameObject.SetActive(false);
            else
                gameObject.SetActive(true);
        }
    }
}