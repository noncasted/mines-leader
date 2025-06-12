using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Services
{
    [DisallowMultipleComponent]
    public class GameCamera : MonoBehaviour, IGameCamera, ISceneService, IScopeSetup
    {
        [SerializeField] private Camera _camera;
        
        private IUpdater _updater;

        public Camera Camera => _camera;

        [Inject]
        private void Construct(IUpdater updater)
        {
            _updater = updater;
        }
        
        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IGameCamera>()
                .As<IScopeSetup>();
        }
        
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
        }

        public void SetSize(float size, float time)
        {
            var start = _camera.orthographicSize;

            _updater.Progression(this.GetObjectLifetime(), time, progress =>
            {
                var newSize = Mathf.Lerp(start, size, progress);
                _camera.orthographicSize = newSize;
            }).Forget();
        }
    }
}